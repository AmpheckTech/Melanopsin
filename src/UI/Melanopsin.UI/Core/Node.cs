// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;

namespace Melanopsin.UI;

/// <summary>
/// Base type for nodes in a compiled UI tree.
/// </summary>
public abstract partial class Node : IContextStore {
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

    /// <summary>
    /// Raised after runtime-owned focus moves to or away from this node.
    /// </summary>
    public event Action<bool>? FocusChanged;

    /// <summary>
    /// Raised when the secondary mouse button requests a context menu. The
    /// event bubbles from the hit target to its ancestors.
    /// </summary>
    public event Action<ContextMenuRequest>? ContextRequested;

    /// <summary>
    /// Raised when the middle mouse button completes a click. The event
    /// bubbles from the hit target to its ancestors.
    /// </summary>
    public event Action<MiddleClickRequest>? MiddleClicked;

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
            FocusChanged?.Invoke(value);
            InvalidateState();
        }
    }

    public bool IsHovered {
        get => _isHovered;
        internal set {
            if (_isHovered == value) {
                return;
            }

            _isHovered = value;
            InvalidateState();
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
                InvalidateState();
            }

            return;
        }

        _states ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        if (_states.TryGetValue(state, out var current) && current) {
            return;
        }

        _states[state] = true;
        InvalidateState();
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
    /// Marks this node as a relayout boundary for damage diagnostics and cached
    /// hit-region reuse. It does not affect measurement-cache reuse.
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

    internal void InvalidateState() {
        Invalidate(
            InvalidationKind.Paint | InvalidationKind.Style,
            LayoutInvalidationReason.Style,
            affectsStyle: true);
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

    internal void RaiseContextRequested(ContextMenuRequest request) {
        ContextRequested?.Invoke(request);
    }

    internal void RaiseMiddleClicked(MiddleClickRequest request) {
        MiddleClicked?.Invoke(request);
    }
}
