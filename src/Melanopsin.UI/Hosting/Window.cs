using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Melanopsin.UI;
using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;
using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed class Window {
    private readonly Func<RenderTargetDescriptor, IRenderTarget> _targetFactory;
    private readonly WindowRenderContext _renderContext;
    private IRenderTarget _target;
    private LayoutSnapshot? _lastLayout;
    private Node? _root;
    private DamageRegionBuilder _pendingDamage;
    private bool _isDirty = true;
    private bool _isLayoutDirty = true;
    private bool _invalidatePaintCache = true;
    private bool _invalidateLayoutCache = true;
    private bool _viewportChanged;
    private bool _forceFullLayoutInvalidation = true;
    private bool _forceFullLayoutDamage = true;
    private readonly HashSet<Node> _invalidatedLayoutNodes =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Node, LayoutNodeGeometry> _previousLayoutGeometry =
        new(ReferenceEqualityComparer.Instance);
    private RenderOptions _lastRenderOptions;
    private bool _hasRendered;
    private long _frameNumber;
    private DevToolsSnapshot? _devToolsSnapshot;
    private UiRuntime? _runtime;
    private bool _disposed;

    public event EventHandler<WindowLifecycleEventArgs>? LifecycleEvent;

    public event Action<Window, DevToolsSnapshot>? DiagnosticsChanged;

    internal Window(
        WindowDescriptor descriptor,
        IRenderTarget target,
        Func<RenderTargetDescriptor, IRenderTarget> targetFactory,
        WindowRenderContext renderContext,
        float scaleFactor) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetFactory);
        ArgumentNullException.ThrowIfNull(renderContext);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleFactor);
        Descriptor = descriptor;
        DisplayScale = scaleFactor;
        _target = target;
        _targetFactory = targetFactory;
        _renderContext = renderContext;
        _pendingDamage = new DamageRegionBuilder(descriptor.Size);
    }

    public WindowId Id => Descriptor.Id;

    public string Title => Descriptor.Title;

    public PixelSize Size => Descriptor.Size;

    public float DisplayScale { get; private set; }

    public WindowRenderContext RenderContext => _renderContext;

    internal UiRuntime? Runtime => _runtime;

    public bool IsDirty => _isDirty;

    public bool IsLayoutDirty => _isLayoutDirty;

    public LayoutSnapshot? Layout => _lastLayout;

    public RenderStatistics LastRenderStatistics { get; private set; }

    public LayoutStatistics LastLayoutStatistics =>
        _lastLayout?.Statistics ?? default;

    public long LayoutVersion { get; private set; }

    public DevToolsSnapshot? LastDiagnostics => _devToolsSnapshot;

    public bool IsDisposed => _disposed;

    public Node Root {
        get => _root ?? throw new InvalidOperationException(
            $"Window '{Title}' has no root. Set Root before rendering.");
        set {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            var previous = _root;
            if (ReferenceEquals(previous, value)) {
                return;
            }

            if (value.Parent is not null) {
                throw new InvalidOperationException(
                    "A window root must be a detached node without a parent.");
            }

            if (value.IsMounted) {
                throw new InvalidOperationException(
                    "A window root cannot already be mounted by another owner.");
            }

            var runtime = _runtime;
            List<ComponentHost>? attachedHosts = null;
            try {
                if (runtime is not null) {
                    attachedHosts = AttachRuntimeToTree(value, runtime);
                }

                value.MountTree();
            } catch (Exception exception) {
                var failures = new List<Exception> { exception };
                try {
                    value.UnmountTree();
                } catch (Exception cleanupException) {
                    failures.Add(cleanupException);
                }

                DetachRuntimes(attachedHosts, runtime, failures);
                ThrowIfCleanupFailed(
                    "The new root failed to mount and could not be cleaned up.",
                    failures);
            }

            try {
                if (previous is not null) {
                    previous.Invalidated -= OnRootInvalidated;
                    previous.UnmountTree();
                }

                _root = value;
                value.Invalidated += OnRootInvalidated;
                InvalidateLayout(forceFullDamage: true);
            } catch (Exception exception) {
                var failures = new List<Exception> { exception };
                try {
                    value.Invalidated -= OnRootInvalidated;
                    value.UnmountTree();
                } catch (Exception cleanupException) {
                    failures.Add(cleanupException);
                }

                DetachRuntimes(attachedHosts, runtime, failures);

                if (previous is not null) {
                    try {
                        previous.Invalidated += OnRootInvalidated;
                        previous.MountTree();
                    } catch (Exception rollbackException) {
                        failures.Add(rollbackException);
                    }
                }

                _root = previous;
                if (failures.Count == 1) {
                    throw;
                }

                throw new AggregateException(
                    "The root replacement failed and could not be rolled back completely.",
                    failures);
            }
        }
    }

    internal void AttachRuntime(UiRuntime runtime) {
        ArgumentNullException.ThrowIfNull(runtime);
        if (_runtime is not null && !ReferenceEquals(_runtime, runtime)) {
            throw new InvalidOperationException(
                "A window cannot be attached to more than one UiRuntime.");
        }

        if (_root is not null) {
            AttachRuntimeToTree(_root, runtime);
        }

        _runtime = runtime;
    }

    public void Resize(PixelSize size) {
        ThrowIfDisposed();
        if (size == Descriptor.Size) {
            return;
        }

        var previousDescriptor = Descriptor;
        var targetDescriptor = new RenderTargetDescriptor(size);
        if (_target is IResizableRenderTarget resizable &&
            resizable.TryResize(targetDescriptor)) {
            Descriptor = new WindowDescriptor(
                previousDescriptor.Id,
                previousDescriptor.Title,
                size);
            _pendingDamage = new DamageRegionBuilder(size);
            InvalidateViewport(forceFullDamage: true);
            return;
        }

        var replacement = _targetFactory(targetDescriptor);
        var previous = _target;
        _target = replacement;
        Descriptor = new WindowDescriptor(
                previousDescriptor.Id,
                previousDescriptor.Title,
                size);
        try {
            previous.Dispose();
        } catch {
            replacement.Dispose();
            _target = previous;
            Descriptor = previousDescriptor;
            throw;
        }

        _pendingDamage = new DamageRegionBuilder(size);
        InvalidateViewport(forceFullDamage: true);
    }

    public void SetDisplayScale(float scaleFactor) {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleFactor);
        if (Math.Abs(DisplayScale - scaleFactor) < 0.0001f) {
            return;
        }

        DisplayScale = scaleFactor;
        InvalidateLayout(forceFullDamage: true);
    }

    public void Invalidate() {
        InvalidateLayout();
    }

    /// <summary>
    /// Schedules composition while retaining the current layout snapshot.
    /// </summary>
    public void InvalidatePaint() {
        ThrowIfDisposed();
        _isDirty = true;
        _invalidatePaintCache = true;
        _pendingDamage.MarkFull();
    }

    /// <summary>
    /// Schedules measure, arrange and composition on the next render.
    /// </summary>
    public void InvalidateLayout() {
        InvalidateLayout(forceFullDamage: false);
    }

    private void InvalidateLayout(bool forceFullDamage) {
        ThrowIfDisposed();
        MarkLayoutDirty(
            invalidateLayoutCache: true,
            invalidatePaintCache: true,
            forceFullDamage: forceFullDamage);
    }

    private void InvalidateViewport(bool forceFullDamage) {
        _viewportChanged = true;
        MarkLayoutDirty(
            invalidateLayoutCache: false,
            invalidatePaintCache: true,
            forceFullDamage: forceFullDamage);
    }

    private void MarkLayoutDirty(
        bool invalidateLayoutCache,
        bool invalidatePaintCache,
        bool forceFullDamage) {
        _isDirty = true;
        _isLayoutDirty = true;
        _invalidatePaintCache |= invalidatePaintCache;
        _invalidateLayoutCache |= invalidateLayoutCache;
        if (invalidateLayoutCache) {
            _forceFullLayoutInvalidation = true;
            _invalidatedLayoutNodes.Clear();
        }

        _forceFullLayoutDamage |= forceFullDamage;
        if (forceFullDamage) {
            _pendingDamage.MarkFull();
        }
    }

    public void Invalidate(InvalidationKind kind) {
        ThrowIfDisposed();
        switch (kind) {
            case InvalidationKind.None:
                return;
            case InvalidationKind.Build:
                _isDirty = true;
                return;
            case InvalidationKind.Paint:
                InvalidatePaint();
                return;
            default:
                InvalidateLayout();
                return;
        }
    }

    public HitRegion? HitTest(float x, float y) {
        ThrowIfDisposed();
        return _lastLayout?.HitTest(x, y);
    }

    internal RenderFrame Render(RenderOptions options) {
        ThrowIfDisposed();
        options = options with {
            ScaleFactor = DisplayScale,
        };
        _renderContext.UpdateMetrics(Size, DisplayScale);
        if (_hasRendered && options != _lastRenderOptions) {
            _invalidatePaintCache = true;
            _forceFullLayoutDamage = true;
            _pendingDamage.MarkFull();
        }

        var layoutRan = false;
        var layoutMilliseconds = 0d;
        FlushPendingBuilds(Root);
        var previousLayout = _lastLayout;
        var canDiffLayout = _isLayoutDirty &&
            previousLayout is not null &&
            !_forceFullLayoutDamage;
        if (canDiffLayout) {
            CaptureLayoutGeometry(previousLayout!);
        }

        if (_isLayoutDirty || _lastLayout is null) {
            var layoutStart = Stopwatch.GetTimestamp();
            _lastLayout = _renderContext.Layout.Layout(
                Root,
                Size,
                new LayoutPassContext(
                    invalidateCache: _invalidateLayoutCache,
                    viewportChanged: _viewportChanged,
                    invalidatedLayoutNodes: _invalidatedLayoutNodes,
                    forceFullInvalidation: _forceFullLayoutInvalidation));
            _isLayoutDirty = false;
            _invalidateLayoutCache = false;
            _viewportChanged = false;
            _forceFullLayoutInvalidation = false;
            _invalidatedLayoutNodes.Clear();
            LayoutVersion++;
            layoutRan = true;
            layoutMilliseconds = Stopwatch.GetElapsedTime(layoutStart).TotalMilliseconds;
        }

        if (layoutRan) {
            if (_forceFullLayoutDamage || !canDiffLayout) {
                _previousLayoutGeometry.Clear();
                _pendingDamage.MarkFull();
            } else {
                AddLayoutDamage(_lastLayout!);
            }
        }

        var layout = _lastLayout ?? throw new InvalidOperationException(
            "The layout pass completed without producing a layout snapshot.");
        var damage = _pendingDamage.Build();
        if (damage.IsEmpty && !_hasRendered) {
            damage = DamageRegion.Full(Size);
        }

        var damageArea = damage.Rects.Sum(static rect => rect.Area);
        double composeMilliseconds;
        double renderMilliseconds;
        RenderFrame frame;
        try {
            var composeStart = Stopwatch.GetTimestamp();
            var scene = _renderContext.Composer.Compose(
                layout,
                new RenderCompositionContext(
                    options,
                    damage,
                    _invalidatePaintCache));
            composeMilliseconds =
                Stopwatch.GetElapsedTime(composeStart).TotalMilliseconds;
            var renderStart = Stopwatch.GetTimestamp();
            frame = _target.Render(scene);
            renderMilliseconds =
                Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
        } catch {
            throw;
        }

        if (frame.Descriptor.Size != Size) {
            throw new InvalidOperationException(
                "The rendering backend returned a frame for the wrong size.");
        }

        _pendingDamage = new DamageRegionBuilder(Size);
        _invalidatePaintCache = false;
        _forceFullLayoutDamage = false;
        _lastRenderOptions = options;
        _hasRendered = true;
        LastRenderStatistics = frame.Statistics;
        _frameNumber++;
        _devToolsSnapshot = new DevToolsSnapshot(
            _frameNumber,
            LayoutVersion,
            frame.Statistics.CommandCount,
            damage.Rects.Count,
            damageArea,
            frame.Statistics.ReusedPaintBoundaries,
            frame.Statistics.RepaintedPaintBoundaries,
            frame.Statistics.RasterCacheHits,
            frame.Statistics.RasterCacheMisses,
            frame.Statistics.RasterCacheBytes,
            layout.Statistics.InvalidationScope,
            layout.Statistics.InvalidationPathNodeCount,
            layout.Statistics.RelayoutBoundaryHits,
            layout.Statistics.RelayoutBoundaryMisses,
            layout.Statistics.ArrangeReuseHits,
            layout.Statistics.ArrangeReuseMisses,
            layout.Statistics.HitRegionCacheHits,
            layout.Statistics.HitRegionCacheMisses,
            layout.Statistics.MeasureExecutions,
            layout.Statistics.PersistentMeasureHits,
            layoutMilliseconds,
            composeMilliseconds,
            renderMilliseconds,
            options.VisualAccessibility);
        DiagnosticsChanged?.Invoke(this, _devToolsSnapshot.Value);
        return frame;
    }

    internal void MarkClean() {
        _isDirty = false;
    }

    internal bool AdvanceAnimations(float elapsedSeconds) {
        ThrowIfDisposed();
        var changed = _renderContext.AdvanceAnimations(Root, elapsedSeconds);
        changed |= AdvanceNodeAnimations(Root, elapsedSeconds);
        return changed;
    }

    internal void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        List<Exception>? failures = null;
        if (_root is not null) {
            try {
                _root.Invalidated -= OnRootInvalidated;
                _root.UnmountTree();
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            } finally {
                _root = null;
            }
        }

        try {
            _target.Dispose();
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            LifecycleEvent?.Invoke(this, new WindowLifecycleEventArgs(
                WindowLifecycleKind.Closed,
                Id,
                Size));
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        if (failures is not null) {
            throw new AggregateException(
                "The window failed to dispose cleanly.",
                failures);
        }
    }

    internal void RaiseLifecycle(
        WindowLifecycleKind kind,
        int x = 0,
        int y = 0) {
        LifecycleEvent?.Invoke(this, new WindowLifecycleEventArgs(
            kind,
            Id,
            Size,
            x,
            y));
    }

    public WindowDescriptor Descriptor { get; private set; }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void OnRootInvalidated(NodeInvalidation invalidation) {
        if (_disposed) {
            return;
        }

        var source = invalidation.Source;
        var kind = invalidation.Kind;
        if ((kind & InvalidationKind.Build) != 0) {
            _isDirty = true;
        }

        if ((kind & (InvalidationKind.Paint | InvalidationKind.Layout)) == 0) {
            return;
        }

        if ((kind & InvalidationKind.Layout) != 0) {
            if (invalidation.LayoutReason == LayoutInvalidationReason.LayoutProperty) {
                _invalidatedLayoutNodes.Add(source);
            } else {
                _forceFullLayoutInvalidation = true;
                _invalidatedLayoutNodes.Clear();
            }

            if (_lastLayout is { } currentLayout &&
                currentLayout.TryGetNode(source, out var currentLayoutNode)) {
                AddDamage(currentLayout, currentLayoutNode);
            }

            MarkLayoutDirty(
                invalidateLayoutCache: false,
                invalidatePaintCache: false,
                forceFullDamage: false);
            return;
        }

        if (_renderContext.Styles.StateChangeRequiresLayout(source)) {
            source.MarkLayoutChanged();
            _forceFullLayoutInvalidation = true;
            _invalidatedLayoutNodes.Clear();
            MarkLayoutDirty(
                invalidateLayoutCache: false,
                invalidatePaintCache: false,
                forceFullDamage: false);
            return;
        }

        _isDirty = true;
        if (_isLayoutDirty ||
            _lastLayout is null ||
            !_lastLayout.TryGetNode(source, out var layoutNode)) {
            _invalidatePaintCache = true;
            _pendingDamage.MarkFull();
            return;
        }

        var damageBounds = GetVisibleBounds(_lastLayout, layoutNode);
        if (damageBounds.IsEmpty) {
            return;
        }

        damageBounds = damageBounds.Inflate(1);
        _pendingDamage.Add(damageBounds);
    }

    private void CaptureLayoutGeometry(LayoutSnapshot layout) {
        _previousLayoutGeometry.Clear();
        foreach (var node in layout.Nodes) {
            if (!layout.TryGetNode(node, out var layoutNode)) {
                continue;
            }

            _previousLayoutGeometry[node] = new LayoutNodeGeometry(
                layoutNode.Bounds,
                GetVisibleBounds(layout, layoutNode),
                layoutNode.ClipBounds,
                layoutNode.Parent,
                layoutNode.SiblingIndex);
        }
    }

    private void AddLayoutDamage(LayoutSnapshot layout) {
        foreach (var previous in _previousLayoutGeometry) {
            if (!layout.Contains(previous.Key)) {
                AddDamage(previous.Value.VisibleBounds);
            }
        }

        foreach (var node in layout.Nodes) {
            if (!layout.TryGetNode(node, out var layoutNode)) {
                continue;
            }

            var current = new LayoutNodeGeometry(
                layoutNode.Bounds,
                GetVisibleBounds(layout, layoutNode),
                layoutNode.ClipBounds,
                layoutNode.Parent,
                layoutNode.SiblingIndex);
            if (!_previousLayoutGeometry.TryGetValue(node, out var previous)) {
                AddDamage(current.VisibleBounds);
                continue;
            }

            if (previous.Bounds != current.Bounds ||
                previous.VisibleBounds != current.VisibleBounds ||
                previous.ClipBounds != current.ClipBounds ||
                !ReferenceEquals(previous.Parent, current.Parent) ||
                previous.SiblingIndex != current.SiblingIndex) {
                AddDamage(previous.VisibleBounds);
                AddDamage(current.VisibleBounds);
            }
        }

        _previousLayoutGeometry.Clear();
    }

    private void AddDamage(LayoutSnapshot layout, LayoutNode layoutNode) {
        AddDamage(GetVisibleBounds(layout, layoutNode));
    }

    private void AddDamage(Rect bounds) {
        if (bounds.IsEmpty) {
            return;
        }

        _pendingDamage.Add(bounds.Inflate(1));
    }

    private static Rect GetVisibleBounds(
        LayoutSnapshot layout,
        LayoutNode layoutNode) {
        var bounds = ToWindowBounds(layoutNode);
        for (var ancestor = layoutNode.Parent;
             ancestor is not null;
             ancestor = ancestor.Parent) {
            if (ancestor.Node is not Control { ClipToBounds: true }) {
                continue;
            }

            bounds = bounds.Intersect(ToWindowBounds(ancestor));
        }

        return bounds;
    }

    private static Rect ToWindowBounds(LayoutNode layoutNode) {
        var x = layoutNode.Bounds.X;
        var y = layoutNode.Bounds.Y;
        for (var ancestor = layoutNode.Parent;
             ancestor is not null;
             ancestor = ancestor.Parent) {
            if (ancestor.Node is IScrollTransform scrollTransform) {
                x -= scrollTransform.OffsetX;
                y -= scrollTransform.OffsetY;
            }
        }

        return new Rect(
            x,
            y,
            layoutNode.Bounds.Width,
            layoutNode.Bounds.Height);
    }

    private static bool AdvanceNodeAnimations(Node node, float elapsedSeconds) {
        var changed = node is ScrollViewport viewport &&
            viewport.AdvanceAnimation(elapsedSeconds);
        changed |= node is PresenceScope presence &&
            presence.AdvanceAnimation(elapsedSeconds);
        if (node is Element element) {
            foreach (var child in element.Children) {
                changed |= AdvanceNodeAnimations(child, elapsedSeconds);
            }
        }

        return changed;
    }

    private static void FlushPendingBuilds(Node node) {
        if (node is ComponentHost host) {
            host.FlushPendingBuild();
        }

        if (node is Element element) {
            foreach (var child in element.Children) {
                FlushPendingBuilds(child);
            }
        }
    }

    private void AttachRuntimeToTree(Node root) {
        var runtime = _runtime ?? throw new InvalidOperationException(
            "A window runtime must be attached before propagating it to a root tree.");
        AttachRuntimeToTree(root, runtime);
    }

    private static List<ComponentHost>? AttachRuntimeToTree(
        Node node,
        UiRuntime runtime) {
        var hosts = new List<ComponentHost>();
        CollectComponentHosts(node, hosts);
        foreach (var host in hosts) {
            if (host.Runtime is not null && !ReferenceEquals(host.Runtime, runtime)) {
                throw new InvalidOperationException(
                    "A component host cannot be attached to more than one UiRuntime.");
            }
        }

        List<ComponentHost>? attached = null;
        foreach (var host in hosts) {
            if (host.Runtime is null) {
                host.AttachRuntime(runtime);
                (attached ??= []).Add(host);
            }
        }

        return attached;
    }

    private static void CollectComponentHosts(Node node, List<ComponentHost> hosts) {
        if (node is ComponentHost host) {
            hosts.Add(host);
        }

        if (node is not Element element) {
            return;
        }

        foreach (var child in element.Children) {
            CollectComponentHosts(child, hosts);
        }
    }

    private static void DetachRuntimes(
        IReadOnlyList<ComponentHost>? hosts,
        UiRuntime? runtime,
        List<Exception> failures) {
        if (hosts is null || runtime is null) {
            return;
        }

        for (var index = hosts.Count - 1; index >= 0; index--) {
            try {
                hosts[index].DetachRuntime(runtime);
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
    }

    private static void ThrowIfCleanupFailed(
        string message,
        List<Exception> failures) {
        if (failures.Count > 1) {
            throw new AggregateException(message, failures);
        }

        ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }

    private readonly record struct LayoutNodeGeometry(
        Rect Bounds,
        Rect VisibleBounds,
        Rect? ClipBounds,
        LayoutNode? Parent,
        int SiblingIndex);
}
