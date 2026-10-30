using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Styling;

public sealed class StyleEngine {
    private readonly StyleSheet _sheet;
    private UiMetrics _metrics;
    private StyleDeclarationParser _declarations;
    private readonly Dictionary<Node, StyleTransitionState> _transitions = [];
    private readonly Dictionary<Node, CachedStyle> _styleCache = [];

    public StyleEngine(StyleSheet? sheet = null, UiMetrics? metrics = null) {
        _sheet = sheet ?? MaterialStyleSheet.Create();
        _metrics = metrics ?? UiMetrics.Default;
        _declarations = new StyleDeclarationParser(_metrics);
    }

    public UiMetrics Metrics => _metrics;

    public StyleSheet Sheet => _sheet;

    internal void UpdateMetrics(UiMetrics metrics) {
        if (_metrics == metrics) {
            return;
        }

        var scaleChanged =
            _metrics.ScaleFactor != metrics.ScaleFactor ||
            _metrics.RootFontSize != metrics.RootFontSize;
        _metrics = metrics;
        _declarations = new StyleDeclarationParser(metrics);
        if (scaleChanged) {
            _styleCache.Clear();
            _transitions.Clear();
        }
    }

    public int ActiveTransitionCount => _transitions.Values.Count(
        static transition => !transition.IsComplete);

    public bool StateChangeRequiresLayout(Node node) {
        ArgumentNullException.ThrowIfNull(node);
        return StyleSelector.StateChangeRequiresLayout(_sheet, node);
    }

    /// <summary>
    /// Resolves the style currently visible to layout and composition. Reading a
    /// style never starts or advances a transition.
    /// </summary>
    public ResolvedStyle Resolve(Node node) {
        ArgumentNullException.ThrowIfNull(node);
        return ResolvedStyleInterpolator.Clone(ResolveReadOnly(node));
    }

    /// <summary>
    /// Resolves the effective cursor for a hit route. The nearest explicit
    /// cursor declaration wins; otherwise the platform default is used.
    /// </summary>
    public CursorKind ResolveCursor(WindowHitTarget target) {
        ArgumentNullException.ThrowIfNull(target);
        for (var index = 0; index < target.Route.Count; index++) {
            var cursor = ResolveTarget(target.Route[index]).Cursor;
            if (cursor is { } explicitCursor) {
                return explicitCursor;
            }
        }

        return CursorKind.Auto;
    }

    internal ResolvedStyle ResolveReadOnly(Node node) {
        ArgumentNullException.ThrowIfNull(node);
        var target = ResolveTarget(node);
        if (_transitions.TryGetValue(node, out var transition)) {
            return transition.Current;
        }

        var cached = _styleCache[node];
        return cached.PendingTransitionSource is { } source
            ? ResolvedStyleInterpolator.Interpolate(source, target, amount: 0)
            : target;
    }

    private ResolvedStyle ResolveTarget(Node node) {
        if (_styleCache.TryGetValue(node, out var cached) &&
            cached.IsValid(node, _sheet, _metrics)) {
            return cached.Style;
        }

        var style = ComputeStyle(node);
        ResolvedStyle? transitionSource = null;
        if (_styleCache.TryGetValue(node, out var previous)) {
            var source = previous.PendingTransitionSource ?? previous.Style;
            var invalidation = ResolvedStyleComparer.GetInvalidationKind(source, style);
            if (!ResolvedStyleComparer.AreEquivalent(source, style) &&
                style.TransitionDuration > 0 &&
                (invalidation & InvalidationKind.Paint) != 0) {
                transitionSource = source;
            }
        }

        _styleCache[node] = new CachedStyle(
            node.StyleRevision,
            _sheet.Version,
            _metrics,
            style,
            transitionSource);
        return style;
    }

    private ResolvedStyle ComputeStyle(Node node) {
        var style = new ResolvedStyle {
            Padding = _metrics.Space(2),
            FontSize = _metrics.Font(UiMetrics.DefaultFontSize),
        };
        var rules = _sheet.Rules
            .Where(rule => StyleSelector.Matches(rule.Selector, node))
            .OrderBy(static rule => rule.Specificity);
        foreach (var rule in rules) {
            _declarations.Apply(style, rule.Declarations);
        }

        if (node is Element element) {
            _declarations.ApplyInline(style, element);
        }

        return style;
    }

    /// <summary>
    /// Advances style transitions for one frame. This is the only phase allowed
    /// to create, retarget or complete transition state.
    /// </summary>
    public bool AdvanceAnimations(Node root, float elapsedSeconds) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedSeconds);
        var hasAnimationWork = false;
        AdvanceAnimations(root, elapsedSeconds, ref hasAnimationWork);
        return hasAnimationWork;
    }

    private void AdvanceAnimations(
        Node node,
        float elapsedSeconds,
        ref bool hasAnimationWork) {
        var target = ResolveTarget(node);
        if (_transitions.TryGetValue(node, out var transition)) {
            _styleCache[node].ClearPendingTransitionSource();
            if (!ResolvedStyleComparer.AreEquivalent(transition.Target, target)) {
                transition.Retarget(target);
            }

            if (AdvanceTransition(
                    node,
                    transition,
                    elapsedSeconds,
                    ref hasAnimationWork)) {
                _transitions.Remove(node);
            }
        } else if (_styleCache[node].TakePendingTransitionSource() is { } source) {
            if (target.TransitionDuration > 0 &&
                (ResolvedStyleComparer.GetInvalidationKind(source, target) &
                    InvalidationKind.Paint) != 0) {
                transition = new StyleTransitionState(source, target);
                _transitions[node] = transition;
                if (AdvanceTransition(
                        node,
                        transition,
                        elapsedSeconds,
                        ref hasAnimationWork)) {
                    _transitions.Remove(node);
                }
            }
        }

        if (node is Element element) {
            foreach (var child in element.Children) {
                AdvanceAnimations(child, elapsedSeconds, ref hasAnimationWork);
            }
        }
    }

    private static bool AdvanceTransition(
        Node node,
        StyleTransitionState transition,
        float elapsedSeconds,
        ref bool hasAnimationWork) {
        var previous = transition.Current;
        _ = transition.Advance(elapsedSeconds);
        var current = transition.IsComplete
            ? ResolvedStyleInterpolator.Clone(transition.Target)
            : ResolvedStyleInterpolator.Interpolate(
                transition.Source,
                transition.Target,
                transition.EasedProgress);
        transition.SetCurrent(current);

        var isActive = !transition.IsComplete;
        var invalidation = ResolvedStyleComparer.GetInvalidationKind(previous, current);
        if (invalidation == InvalidationKind.None && isActive) {
            invalidation = ResolvedStyleComparer.GetInvalidationKind(current, transition.Target);
        }

        if (invalidation != InvalidationKind.None) {
            node.InvalidateFromPresentation(invalidation);
        }

        hasAnimationWork |= isActive || invalidation != InvalidationKind.None;
        return !isActive;
    }

    internal void Prune(IReadOnlySet<Node> liveNodes) {
        ArgumentNullException.ThrowIfNull(liveNodes);
        RemoveStaleEntries(_styleCache, liveNodes);
        RemoveStaleEntries(_transitions, liveNodes);
    }

    private static void RemoveStaleEntries<T>(
        Dictionary<Node, T> entries,
        IReadOnlySet<Node> liveNodes) {
        if (entries.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in entries.Keys) {
            if (liveNodes.Contains(node)) {
                continue;
            }

            stale ??= [];
            stale.Add(node);
        }

        if (stale is null) {
            return;
        }

        foreach (var node in stale) {
            entries.Remove(node);
        }
    }

}
