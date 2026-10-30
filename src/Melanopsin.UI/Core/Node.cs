using System.Runtime.ExceptionServices;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;

namespace Melanopsin.UI;

/// <summary>
/// Base type for nodes in a compiled UI tree.
/// </summary>
public abstract class Node : IContextStore {
    private static long s_nextAccessibilityId;
    private bool _isRepaintBoundary;
    private bool _allowsRasterCache = true;
    private bool _isRelayoutBoundary;
    private bool _isFocused;
    private bool _isHovered;
    private bool _isMounted;
    private CancellationTokenSource? _mountCancellation;
    private List<IDisposable>? _mountResources;
    private Dictionary<string, bool>? _states;
    private Dictionary<object, object>? _contextValues;
    private long _accessibilityId;

    internal Node() {
    }

    public Node? Parent { get; private set; }

    public bool IsMounted => _isMounted;

    internal AccessibilityNodeId AccessibilityId => new(
        _accessibilityId != 0
            ? _accessibilityId
            : _accessibilityId = Interlocked.Increment(ref s_nextAccessibilityId));

    Dictionary<object, object>? IContextStore.ContextValues {
        get => _contextValues;
        set => _contextValues = value;
    }

    public CancellationToken MountToken {
        get {
            if (!_isMounted) {
                return CancellationToken.None;
            }

            return (_mountCancellation ??= new CancellationTokenSource()).Token;
        }
    }

    public IInputHandler? InputHandler { get; set; }

    public event Action<PointerInputEvent>? PointerEntered;

    public event Action<PointerInputEvent>? PointerExited;

    public event Action<PointerInputEvent>? PointerMoved;

    public event Action<PointerInputEvent>? PointerPressed;

    public event Action<PointerInputEvent>? PointerReleased;

    public event Action<Node>? Mounted;

    public event Action<Node>? Unmounted;

    /// <summary>
    /// Raised when this node or a descendant requests a pipeline invalidation.
    /// The source remains the node that originated the request while the
    /// notification bubbles to the parent.
    /// </summary>
    public event Action<NodeInvalidation>? Invalidated;

    public int ReconciliationSlot { get; set; } = -1;

    public object? ReconciliationKey { get; set; }

    public bool IsFocused {
        get => _isFocused;
        internal set {
            if (_isFocused == value) {
                return;
            }

            _isFocused = value;
            OnFocusChanged(value);
            InvalidatePaint();
        }
    }

    public bool IsHovered {
        get => _isHovered;
        internal set {
            if (_isHovered == value) {
                return;
            }

            _isHovered = value;
            InvalidatePaint();
        }
    }

    /// <summary>
    /// Called after the runtime-owned focus state changes.
    /// </summary>
    protected internal virtual void OnFocusChanged(bool focused) {
    }

    /// <summary>
    /// Returns whether a built-in or custom semantic state is active.
    /// </summary>
    public bool HasState(string state) {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        return state switch {
            UiStateNames.Hover => IsHovered,
            UiStateNames.Focused => IsFocused,
            UiStateNames.Disabled => this is Control { IsEnabled: false },
            _ => _states is not null &&
                 _states.TryGetValue(state, out var enabled) &&
                 enabled,
        };
    }

    /// <summary>
    /// Sets a control-defined state that can participate in style selectors.
    /// Hover, focus and disabled are owned by the input/runtime lifecycle.
    /// </summary>
    public void SetState(string state, bool enabled) {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (state is UiStateNames.Hover or UiStateNames.Focused or UiStateNames.Disabled) {
            throw new ArgumentException(
                $"The '{state}' state is controlled by the UI runtime.",
                nameof(state));
        }

        if (!enabled) {
            if (_states?.Remove(state) == true) {
                InvalidatePaint();
            }

            return;
        }

        _states ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        if (_states.TryGetValue(state, out var current) && current) {
            return;
        }

        _states[state] = true;
        InvalidatePaint();
    }

    public virtual bool IsFocusable { get; protected set; }

    /// <summary>
    /// Caches the paint commands for this subtree independently of its parent.
    /// </summary>
    public bool IsRepaintBoundary {
        get => _isRepaintBoundary;
        set {
            if (_isRepaintBoundary == value) {
                return;
            }

            _isRepaintBoundary = value;
            InvalidatePaint();
        }
    }

    /// <summary>
    /// Gets or sets whether the renderer may cache the raster output of this
    /// repaint boundary. Defaults to <see langword="true"/>. Disabling raster
    /// caching does not disable repaint-boundary layer reuse or invalidation isolation.
    /// </summary>
    public bool AllowsRasterCache {
        get => _allowsRasterCache;
        set {
            if (_allowsRasterCache == value) {
                return;
            }

            _allowsRasterCache = value;
            InvalidatePaint();
        }
    }

    /// <summary>
    /// Marks this node as a relayout boundary for damage diagnostics and
    /// cached hit-region subtree reuse. Cross-pass measurement reuse is not
    /// limited to boundaries, so this flag must not be used as a correctness
    /// switch for the measurement cache.
    /// </summary>
    public bool IsRelayoutBoundary {
        get => _isRelayoutBoundary;
        set {
            if (_isRelayoutBoundary == value) {
                return;
            }

            _isRelayoutBoundary = value;
            InvalidateLayout();
        }
    }

    public long PaintRevision { get; private set; }

    public long StyleRevision { get; private set; }

    public long LayoutRevision { get; private set; }

    internal long SubtreePaintRevision { get; private set; }

    internal long SubtreeLayoutRevision { get; private set; }

    internal long SubtreeStructureRevision { get; private set; }

    /// <summary>
    /// Conservatively requests layout and paint.
    /// </summary>
    public void Invalidate() {
        Invalidate(InvalidationKind.LayoutAndPaint);
    }

    /// <summary>
    /// Requests a new composition when geometry and hit regions remain valid.
    /// </summary>
    public void InvalidatePaint() {
        Invalidate(InvalidationKind.Paint);
    }

    /// <summary>
    /// Requests measure, arrange and paint.
    /// </summary>
    public void InvalidateLayout() {
        Invalidate(
            InvalidationKind.LayoutAndPaint,
            LayoutInvalidationReason.Unknown,
            affectsStyle: true);
    }

    public void Invalidate(InvalidationKind kind) {
        Invalidate(
            kind,
            LayoutInvalidationReason.Unknown,
            affectsStyle: true);
    }

    internal void InvalidateFromPresentation(InvalidationKind kind) {
        Invalidate(
            kind,
            LayoutInvalidationReason.Style,
            affectsStyle: false);
    }

    internal void InvalidateBuild() {
        Invalidate(
            InvalidationKind.Build,
            LayoutInvalidationReason.None,
            affectsStyle: false);
    }

    internal void InvalidateLayout(LayoutInvalidationReason reason) {
        Invalidate(
            InvalidationKind.LayoutAndPaint,
            reason,
            affectsStyle: true);
    }

    internal void Invalidate(
        InvalidationKind kind,
        LayoutInvalidationReason layoutReason,
        bool affectsStyle) {
        if (kind == InvalidationKind.None) {
            return;
        }

        RaiseInvalidated(this, kind, layoutReason, affectsStyle);
    }

    public AccessibilityProperties Accessibility { get; } =
        new(AccessibilityRole.Region);

    /// <summary>
    /// Commits a parent-pointer change. The owning element updates its child
    /// collection first, then calls <see cref="CompleteParentChange"/> to run
    /// observable callbacks against a consistent tree.
    /// </summary>
    internal void SetParentState(Node? parent) {
        Parent = parent;
    }

    internal void CompleteParentChange() {
        OnParentChanged(Parent is not null);
        if (Parent?.IsMounted == true) {
            MountTree();
        } else {
            UnmountTree();
        }
    }

    internal void MountTree() {
        if (_isMounted) {
            return;
        }

        _isMounted = true;
        Exception? failure = null;
        CaptureException(ref failure, OnMounted);
        CaptureException(ref failure, () => Mounted?.Invoke(this));
        MountChildren(ref failure);
        ThrowIfFailed(failure);
    }

    internal void UnmountTree() {
        if (!_isMounted) {
            return;
        }

        Exception? failure = null;
        UnmountChildren(ref failure);
        _isMounted = false;
        if (_isFocused) {
            IsFocused = false;
        }

        CaptureException(ref failure, () => _mountCancellation?.Cancel());
        CaptureException(ref failure, OnUnmounted);
        CaptureException(ref failure, () => Unmounted?.Invoke(this));
        CaptureException(ref failure, DisposeMountResources);
        ThrowIfFailed(failure);
    }

    internal virtual void MountChildren(ref Exception? failure) {
    }

    internal virtual void UnmountChildren(ref Exception? failure) {
    }

    internal void RaiseInvalidated(
        Node source,
        InvalidationKind kind,
        LayoutInvalidationReason layoutReason,
        bool affectsStyle) {
        var parent = Parent;
        if (affectsStyle && ReferenceEquals(source, this)) {
            StyleRevision++;
        }

        if ((kind & InvalidationKind.Paint) != 0) {
            SubtreePaintRevision++;
            if (ReferenceEquals(source, this)) {
                PaintRevision++;
            }
        }

        if ((kind & InvalidationKind.Layout) != 0) {
            SubtreeLayoutRevision++;
            if (layoutReason == LayoutInvalidationReason.Structure) {
                SubtreeStructureRevision++;
            }

            if (ReferenceEquals(source, this)) {
                LayoutRevision++;
            }
        }

        Invalidated?.Invoke(new NodeInvalidation(source, kind, layoutReason));
        parent?.RaiseInvalidated(
            source,
            kind,
            layoutReason,
            affectsStyle: false);
    }

    internal void MarkLayoutChanged() {
        for (var node = this; node is not null; node = node.Parent) {
            node.SubtreeLayoutRevision++;
        }

        LayoutRevision++;
    }

    internal void RaisePointerEvent(PointerInputEvent input) {
        var handler = input.Phase switch {
            PointerPhase.Enter => PointerEntered,
            PointerPhase.Leave => PointerExited,
            PointerPhase.Move => PointerMoved,
            PointerPhase.Down => PointerPressed,
            PointerPhase.Up => PointerReleased,
            _ => null,
        };
        handler?.Invoke(input);
    }

    protected internal virtual void OnMounted() {
    }

    protected internal virtual void OnUnmounted() {
    }

    internal virtual void OnParentChanged(bool hasParent) {
    }

    public void RegisterMountedResource(IDisposable resource) {
        ArgumentNullException.ThrowIfNull(resource);
        if (!_isMounted) {
            throw new InvalidOperationException(
                "A mounted resource can only be registered while the node is mounted.");
        }

        (_mountResources ??= []).Add(resource);
    }

    internal static void CaptureException(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception error) {
            failure = CombineFailures(failure, error);
        }
    }

    internal static void ThrowIfFailed(Exception? failure) {
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void DisposeMountResources() {
        Exception? failure = null;
        if (_mountResources is not null) {
            for (var index = _mountResources.Count - 1; index >= 0; index--) {
                CaptureException(
                    ref failure,
                    _mountResources[index].Dispose);
            }

            _mountResources.Clear();
        }

        CaptureException(ref failure, () => _mountCancellation?.Dispose());

        _mountCancellation = null;
        ThrowIfFailed(failure);
    }

    private static Exception CombineFailures(
        Exception? first,
        Exception second) {
        if (first is null) {
            return second;
        }

        return first is AggregateException aggregate
            ? new AggregateException(
                aggregate.InnerExceptions.Append(second))
            : new AggregateException(first, second);
    }
}
