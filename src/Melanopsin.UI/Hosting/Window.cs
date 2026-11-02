using Melanopsin.Graphics.Rendering;
using Melanopsin.UI;
using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
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

    /// <summary>
    /// Draws a debug outline around every element in this window.
    /// </summary>
    public bool ShowDebugBounds { get; private set; }

    public LayoutSnapshot? Layout => _lastLayout;

    public RenderStatistics LastRenderStatistics { get; private set; }

    public LayoutStatistics LastLayoutStatistics =>
        _lastLayout?.Statistics ?? default;

    public long LayoutVersion { get; private set; }

    public DevToolsSnapshot? LastDiagnostics => _devToolsSnapshot;

    public bool IsDisposed => _disposed;

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

    public void SetShowDebugBounds(bool enabled) {
        ThrowIfDisposed();
        if (ShowDebugBounds == enabled) {
            return;
        }

        ShowDebugBounds = enabled;
        InvalidatePaint();
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

    internal void MarkClean() {
        _isDirty = false;
    }

    public WindowDescriptor Descriptor { get; private set; }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

}
