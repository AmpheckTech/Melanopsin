using System.Runtime.CompilerServices;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Layout;

/// <summary>
/// Initial measure/arrange engine for Page, StackPanel, text content and basic
/// leaf controls. It intentionally stays replaceable by a richer layout engine.
/// </summary>
public sealed class FlowLayoutEngine : ILayoutEngine {
    private readonly StyleEngine _styleEngine;
    private readonly ITextMeasurer _textMeasurer;
    private readonly LayoutMeasurementCache _measurementCache = new();
    private readonly LayoutNodeStore _layoutNodeStore = new();
    private readonly HitRegionCollector _hitRegionCollector = new();
    private readonly ConditionalWeakTable<Element, VisibleChildrenCache>
        _visibleChildren = new();
    private readonly Dictionary<Node, bool> _viewportDependencies =
        new(ReferenceEqualityComparer.Instance);
    private readonly FlowLayoutFallback _fallback;
    private UiMetrics _metrics;
    private int _measureRequests;
    private int _measureCacheHits;
    private int _measureExecutions;
    private int _persistentMeasureHits;
    private int _relayoutBoundaryHits;
    private int _relayoutBoundaryMisses;
    private HashSet<Node>? _invalidationPath;
    private LayoutInvalidationScope _invalidationScope;
    private readonly LayoutMeasureContext _measureContext;

    public FlowLayoutEngine(
        StyleEngine? styleEngine = null,
        ITextMeasurer? textMeasurer = null,
        UiMetrics? metrics = null) {
        _styleEngine = styleEngine ?? new StyleEngine();
        _textMeasurer = textMeasurer ?? DefaultTextMeasurer.Instance;
        _metrics = metrics ?? _styleEngine.Metrics;
        _measureContext = new LayoutMeasureContext(this);
        _fallback = new FlowLayoutFallback(this, _textMeasurer);
    }

    internal UiMetrics Metrics => _metrics;

    public LayoutSnapshot Layout(
        Node root,
        PixelSize size,
        LayoutPassContext context = default) {
        ArgumentNullException.ThrowIfNull(root);
        var metrics = _styleEngine.Metrics with {
            ViewportWidth = size.Width,
            ViewportHeight = size.Height,
        };
        _measurementCache.BeginPass(context.InvalidateCache);
        _viewportDependencies.Clear();
        _layoutNodeStore.BeginPass(
            !context.InvalidateCache && _layoutNodeStore.Count != 0);
        _measureRequests = 0;
        _measureCacheHits = 0;
        _measureExecutions = 0;
        _persistentMeasureHits = 0;
        _relayoutBoundaryHits = 0;
        _relayoutBoundaryMisses = 0;
        _invalidationPath = context.IsFullInvalidation
            ? null
            : BuildInvalidationPath(root, context.InvalidatedLayoutNodes!);
        _invalidationScope = _invalidationPath is null
            ? LayoutInvalidationScope.Full
            : LayoutInvalidationScope.MergedPaths;
        _metrics = metrics;
        var constraint = LayoutConstraints.Loose(size.Width, size.Height);
        MeasureNode(root, constraint);
        var rootLayout = ArrangeNode(
            root,
            new Rect(0, 0, size.Width, size.Height),
            parent: null,
            inheritedClip: null);
        var hitRegions = _hitRegionCollector.Collect(rootLayout);
        var snapshot = new LayoutSnapshot(
            size,
            rootLayout,
            hitRegions,
            new LayoutStatistics(
                MeasureRequests: _measureRequests,
                MeasureCacheHits: _measureCacheHits,
                RelayoutBoundaryHits: _relayoutBoundaryHits,
                RelayoutBoundaryMisses: _relayoutBoundaryMisses,
                ArrangeReuseHits: _layoutNodeStore.ArrangeReuseHits,
                ArrangeReuseMisses: _layoutNodeStore.ArrangeReuseMisses,
                HitRegionCacheHits: _hitRegionCollector.CacheHits,
                HitRegionCacheMisses: _hitRegionCollector.CacheMisses,
                MeasureExecutions: _measureExecutions,
                PersistentMeasureHits: _persistentMeasureHits,
                InvalidationScope: _invalidationScope,
                InvalidationPathNodeCount: _invalidationPath?.Count ?? 0));
        _measurementCache.Prune(snapshot);
        _layoutNodeStore.Prune(snapshot);
        _hitRegionCollector.Prune(snapshot);

        return snapshot;
    }

    internal LayoutSize MeasureNode(Node node, LayoutConstraints constraint) {
        _layoutNodeStore.RememberMeasureConstraints(node, constraint);
        _measureRequests++;
        var key = new MeasurementKey(node, constraint);
        if (_measurementCache.TryGetPassMeasurement(
                key,
                out var cached,
                out _)) {
            _measureCacheHits++;
            return cached;
        }

        if (_measurementCache.TryGetPersistentMeasurement(
                node,
                _styleEngine.Sheet.Version,
                constraint,
                _metrics,
                out var persistentSize,
                out var persistentUsesViewportUnits)) {
            _persistentMeasureHits++;
            if (node.IsRelayoutBoundary) {
                _relayoutBoundaryHits++;
            }

            _measurementCache.StorePassMeasurement(
                key,
                persistentSize,
                persistentUsesViewportUnits);
            return persistentSize;
        }

        if (node.IsRelayoutBoundary) {
            _relayoutBoundaryMisses++;
        }

        _measureExecutions++;
        var usesViewportUnits = UsesViewportUnits(node);
        var measured = MeasureNodeCore(node, constraint);
        _measurementCache.Store(
            key,
            measured,
            node.SubtreeLayoutRevision,
            _styleEngine.Sheet.Version,
            constraint,
            _metrics,
            usesViewportUnits);

        return measured;
    }

    internal float MeasureIntrinsic(
        Node node,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        ArgumentNullException.ThrowIfNull(node);
        if (float.IsNaN(crossAxisExtent) || crossAxisExtent < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(crossAxisExtent),
                crossAxisExtent,
                "An intrinsic cross-axis extent must be non-negative.");
        }

        if (node is ILayoutIntrinsicProvider provider) {
            return Math.Max(
                0,
                provider.MeasureIntrinsic(
                    _measureContext,
                    axis,
                    mode,
                    crossAxisExtent));
        }

        var constraints = axis == LayoutAxis.Horizontal
            ? LayoutConstraints.Loose(
                float.PositiveInfinity,
                crossAxisExtent)
            : LayoutConstraints.Loose(
                crossAxisExtent,
                float.PositiveInfinity);
        var measured = MeasureNode(node, constraints);
        return axis == LayoutAxis.Horizontal
            ? measured.Width
            : measured.Height;
    }

    private LayoutSize MeasureNodeCore(
        Node node,
        LayoutConstraints constraint) {
        if (!IsNodeVisible(node)) {
            return new LayoutSize(0, 0);
        }

        if (node is ILayoutParticipant participant) {
            return constraint.Constrain(
                participant.Measure(_measureContext, constraint));
        }

        if (node is TextNode textNode) {
            var textMetrics = MeasureText(
                textNode.Text,
                _metrics.Font(UiMetrics.DefaultFontSize),
                constraint.MaxWidth);
            return constraint.Constrain(new LayoutSize(
                textMetrics.Width,
                Math.Max(_metrics.Space(6), textMetrics.Height)));
        }

        if (node is ComponentHost host) {
            if (host.Content is null) {
                return new LayoutSize(0, 0);
            }

            var measured = MeasureNode(host.Content, constraint);
            var hostStyle = _styleEngine.ResolveReadOnly(host);
            return constraint.Constrain(new LayoutSize(
                ResolveLength(
                    hostStyle.Width,
                    hostStyle.MinWidth,
                    hostStyle.MaxWidth,
                    constraint.MaxWidth,
                    measured.Width,
                    hostStyle.FontSize),
                ResolveLength(
                    hostStyle.Height,
                    hostStyle.MinHeight,
                    hostStyle.MaxHeight,
                    constraint.MaxHeight,
                    measured.Height,
                    hostStyle.FontSize)));
        }

        if (node is not Element element) {
            return new LayoutSize(0, 0);
        }

        return _fallback.Measure(element, constraint);
    }

    internal ResolvedStyle ResolveStyle(Node node) {
        return _styleEngine.ResolveReadOnly(node);
    }

    internal IReadOnlyList<Node> GetVisibleChildren(Element element) {
        return VisibleChildren(element);
    }

    internal TextMetrics MeasureText(
        string text,
        float fontSize,
        float availableWidth,
        FontFace? font = null) {
        if (_textMeasurer is ITextLayout textLayout) {
            return textLayout.Layout(
                text,
                new TextLayoutOptions(
                    fontSize,
                    Math.Max(1, availableWidth),
                    font: font))
                .ToMetrics();
        }

        return _textMeasurer.MeasureText(text, fontSize);
    }

    internal float ResolveLength(
        Length length,
        Length? minimum,
        Length? maximum,
        float available,
        float fallback,
        float emSize = 0,
        bool clampToAvailable = true) {
        var resolved = length.Resolve(_metrics, available, fallback, emSize);
        if (minimum is { } min) {
            resolved = Math.Max(resolved, min.Resolve(_metrics, available, 0, emSize));
        }

        if (maximum is { } max) {
            resolved = Math.Min(
                resolved,
                max.Resolve(_metrics, available, available, emSize));
        }

        return clampToAvailable
            ? Math.Clamp(resolved, 0, available)
            : Math.Max(0, resolved);
    }

    internal LayoutNode ArrangeNode(
        Node node,
        Rect bounds,
        LayoutNode? parent,
        Rect? inheritedClip) {
        var layoutNode = _layoutNodeStore.GetOrCreate(
            node,
            bounds,
            parent,
            inheritedClip,
            node.SubtreeLayoutRevision,
            _styleEngine.Sheet.Version,
            _metrics,
            UsesViewportUnits(node));

        if (layoutNode.WasArrangementReused) {
            return layoutNode;
        }

        if (!IsNodeVisible(node)) {
            return layoutNode;
        }

        if (node is ComponentHost host) {
            if (host.Content is not null) {
                layoutNode.Children.Add(ArrangeNode(
                    host.Content,
                    bounds,
                    layoutNode,
                    inheritedClip));
            }

            SortChildren(layoutNode);
            return layoutNode;
        }

        if (node is not Element element) {
            return layoutNode;
        }

        _fallback.Arrange(element, bounds, layoutNode, inheritedClip);
        SortChildren(layoutNode);
        return layoutNode;
    }

    private static void SortChildren(LayoutNode layoutNode) {
        if (layoutNode.Children.Count < 2) {
            return;
        }

        for (var index = 0; index < layoutNode.Children.Count; index++) {
            layoutNode.Children[index].SiblingIndex = index;
        }

        layoutNode.Children.Sort(static (left, right) => {
            var zOrder = left.ZIndex.CompareTo(right.ZIndex);
            return zOrder != 0
                ? zOrder
                : left.SiblingIndex.CompareTo(right.SiblingIndex);
        });
    }

    private IReadOnlyList<Node> VisibleChildren(Element element) {
        if (_visibleChildren.TryGetValue(element, out var cached) &&
            cached.StructureRevision == element.SubtreeStructureRevision) {
            return cached.Children;
        }

        var children = new List<Node>(element.Children.Count);
        AddVisibleChildren(element, children);
        var visible = new VisibleChildrenCache(
            element.SubtreeStructureRevision,
            children.ToArray());
        _visibleChildren.Remove(element);
        _visibleChildren.Add(element, visible);
        return visible.Children;
    }

    private bool UsesViewportUnits(Node node) {
        if (_viewportDependencies.TryGetValue(node, out var usesViewportUnits)) {
            return usesViewportUnits;
        }

        if (node is ILayoutViewportDependency dependency) {
            usesViewportUnits = dependency.UsesViewportUnits;
        } else if (node is ILayoutParticipant or ILayoutIntrinsicProvider) {
            // Custom layout participants may read the viewport directly. They
            // opt into viewport-independent caching by implementing the
            // dependency contract.
            usesViewportUnits = true;
        }

        if (node is Element element) {
            usesViewportUnits |= _styleEngine.ResolveReadOnly(node).UsesViewportUnits;
            if (!usesViewportUnits) {
                foreach (var child in element.Children) {
                    if (!UsesViewportUnits(child)) {
                        continue;
                    }

                    usesViewportUnits = true;
                    break;
                }
            }
        }

        _viewportDependencies[node] = usesViewportUnits;
        return usesViewportUnits;
    }

    private sealed class VisibleChildrenCache(
        long structureRevision,
        IReadOnlyList<Node> children) {
        public long StructureRevision { get; } = structureRevision;

        public IReadOnlyList<Node> Children { get; } = children;
    }

    private static HashSet<Node>? BuildInvalidationPath(
        Node root,
        IReadOnlyCollection<Node> invalidatedLayoutNodes) {
        var path = new HashSet<Node>(ReferenceEqualityComparer.Instance) {
            root,
        };
        foreach (var invalidatedNode in invalidatedLayoutNodes) {
            Node? current = invalidatedNode;
            while (current is not null) {
                path.Add(current);
                if (ReferenceEquals(current, root)) {
                    break;
                }

                current = current.Parent;
            }

            if (current is null) {
                return null;
            }
        }

        return path;
    }

    private static void AddVisibleChildren(
        Element parent,
        List<Node> children) {
        foreach (var child in parent.Children) {
            if (!IsNodeVisible(child)) {
                continue;
            }

            if (child is Fragment fragment) {
                AddVisibleChildren(fragment, children);
            } else {
                children.Add(child);
            }
        }
    }

    private static bool IsNodeVisible(Node node) {
        return node is not Control { IsVisible: false };
    }

}
