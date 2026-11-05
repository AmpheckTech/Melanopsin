using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

/// <summary>
/// Tracks independent mouse and touch pointers, preserves capture while a
/// pointer is down, and dispatches routed events from target to root.
/// </summary>
public sealed class InputManager {
    private readonly IWindowHitTester _windows;
    private readonly Dictionary<PointerCaptureKey, PointerCaptureState> _captures = [];
    private readonly Dictionary<PointerCaptureKey, WindowHitTarget> _hoverTargets = [];

    public InputManager(IWindowHitTester windows) {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = windows;
    }

    public event Action<RoutedInputEvent>? PointerRouted;

    public int ActivePointerCount => _captures.Count;

    /// <summary>
    /// Gets the node currently under the primary mouse pointer for style and
    /// cursor resolution.
    /// </summary>
    public bool TryGetMouseTarget(
        WindowId windowId,
        out WindowHitTarget? target) {
        var key = new PointerCaptureKey(
            windowId,
            InputDeviceKind.Mouse,
            new PointerId(1));
        return _hoverTargets.TryGetValue(key, out target);
    }

    public bool Process(PointerInputEvent input) => Process(input, out _);

    internal bool Process(PointerInputEvent input, out bool preventDefaultFocus) {
        ArgumentNullException.ThrowIfNull(input);
        preventDefaultFocus = false;
        var key = new PointerCaptureKey(input.WindowId, input.Device, input.PointerId);
        var hasCapture = _captures.TryGetValue(key, out var capture);
        if (!_windows.ContainsWindow(input.WindowId)) {
            return false;
        }

        if (input.Device == InputDeviceKind.Mouse &&
            input.Phase == PointerPhase.Leave &&
            !hasCapture) {
            if (_hoverTargets.Remove(key, out var leavingTarget)) {
                UpdateHoverRoute(leavingTarget, current: null);
                return Dispatch(input, leavingTarget);
            }

            return false;
        }

        var target = hasCapture
            ? capture!.Target
            : _windows.HitTest(input.WindowId, input.X, input.Y);
        if (target is null) {
            if (input.Device == InputDeviceKind.Mouse &&
                !hasCapture &&
                _hoverTargets.Remove(key, out var leavingTarget)) {
                UpdateHoverRoute(leavingTarget, current: null);
                Dispatch(input.WithPhase(PointerPhase.Leave), leavingTarget);
            }

            if (input.Phase is PointerPhase.Up or PointerPhase.Cancel) {
                _captures.Remove(key);
            }

            return false;
        }

        var dispatched = false;
        if (input.Device == InputDeviceKind.Mouse && !hasCapture) {
            _hoverTargets.TryGetValue(key, out var previousHover);
            if (previousHover?.Node != target.Node) {
                dispatched = true;
                UpdateHoverRoute(previousHover, target);
                if (previousHover is not null) {
                    dispatched |= Dispatch(
                        input.WithPhase(PointerPhase.Leave),
                        previousHover);
                }

                dispatched |= Dispatch(input.WithPhase(PointerPhase.Enter), target);
            }

            _hoverTargets[key] = target;
        }

        if (input.Phase == PointerPhase.Down && !hasCapture) {
            capture = new PointerCaptureState(target);
            _captures.Add(key, capture);
        }

        if (input.Phase == PointerPhase.Down &&
            input.Device == InputDeviceKind.Mouse &&
            input.Button != MouseButtonKind.None) {
            capture!.Buttons.Add(input.Button);
        }

        if (input.Phase != PointerPhase.Enter) {
            dispatched |= Dispatch(input, target, out preventDefaultFocus);
        }

        var completedSecondary = input.Phase == PointerPhase.Up &&
            input.IsSecondaryButton &&
            capture?.Buttons.Contains(MouseButtonKind.Right) == true &&
            IsInside(target, input);
        var completedMiddle = input.Phase == PointerPhase.Up &&
            input.IsMiddleButton &&
            capture?.Buttons.Contains(MouseButtonKind.Middle) == true &&
            IsInside(target, input);

        if (input.Phase == PointerPhase.Up) {
            if (input.Device == InputDeviceKind.Mouse &&
                capture is not null &&
                input.Button != MouseButtonKind.None) {
                capture.Buttons.Remove(input.Button);
                if (capture.Buttons.Count != 0) {
                    DispatchButtonEvent(
                        input,
                        target,
                        completedSecondary,
                        completedMiddle);
                    return dispatched;
                }
            }

            _captures.Remove(key);
        } else if (input.Phase == PointerPhase.Cancel) {
            if (_hoverTargets.Remove(key, out var canceledHover)) {
                UpdateHoverRoute(canceledHover, current: null);
            }

            _captures.Remove(key);
        }

        DispatchButtonEvent(
            input,
            target,
            completedSecondary,
            completedMiddle);

        return dispatched;
    }

    public void CancelWindow(WindowId windowId) {
        var keys = _captures.Keys
            .Where(key => key.WindowId == windowId)
            .ToArray();
        foreach (var key in keys) {
            var capture = _captures[key];
            if (_windows.ContainsWindow(windowId)) {
                var input = new PointerInputEvent(
                    windowId,
                    key.PointerId,
                    key.Device,
                    PointerPhase.Cancel,
                    x: 0,
                    y: 0,
                    isPrimary: false);
                Dispatch(input, capture.Target);
            }

            _captures.Remove(key);
        }

        var hoverKey = new PointerCaptureKey(windowId, InputDeviceKind.Mouse, new PointerId(1));
        if (_hoverTargets.Remove(hoverKey, out var hoverTarget)) {
            UpdateHoverRoute(hoverTarget, current: null);
        }
    }

    private static void UpdateHoverRoute(
        WindowHitTarget? previous,
        WindowHitTarget? current) {
        if (previous is not null) {
            foreach (var node in previous.Route) {
                if (!Contains(current, node)) {
                    node.IsHovered = false;
                }
            }
        }

        if (current is not null) {
            foreach (var node in current.Route) {
                if (!Contains(previous, node)) {
                    node.IsHovered = true;
                }
            }
        }
    }

    private static bool Contains(WindowHitTarget? target, Node node) {
        if (target is null) {
            return false;
        }

        foreach (var candidate in target.Route) {
            if (ReferenceEquals(candidate, node)) {
                return true;
            }
        }

        return false;
    }

    private bool Dispatch(PointerInputEvent input, WindowHitTarget target) {
        return Dispatch(input, target, out _);
    }

    private bool Dispatch(
        PointerInputEvent input,
        WindowHitTarget target,
        out bool preventDefaultFocus) {
        var routedEvent = new RoutedInputEvent(input, target);
        preventDefaultFocus = false;
        PointerRouted?.Invoke(routedEvent);
        preventDefaultFocus |= routedEvent.PreventDefaultFocus;
        var dispatched = false;
        for (var index = target.Route.Count - 1; index >= 0; index--) {
            var currentTarget = target.Route[index];
            routedEvent.CurrentTarget = currentTarget;
            currentTarget.RaisePointerEvent(input);
            dispatched = true;
            if (currentTarget.InputHandler is not { } handler) {
                continue;
            }

            handler.Handle(routedEvent);
            preventDefaultFocus |= routedEvent.PreventDefaultFocus;
            if (routedEvent.Handled) {
                break;
            }
        }

        return dispatched;
    }

    private static void DispatchButtonEvent(
        PointerInputEvent input,
        WindowHitTarget target,
        bool contextRequested,
        bool middleClicked) {
        if (contextRequested) {
            var request = new ContextMenuRequest(target.Node, input);
            for (var index = target.Route.Count - 1; index >= 0; index--) {
                request.CurrentTarget = target.Route[index];
                request.CurrentTarget.RaiseContextRequested(request);
                if (request.Handled) {
                    break;
                }
            }
        }

        if (middleClicked) {
            var request = new MiddleClickRequest(target.Node, input);
            for (var index = target.Route.Count - 1; index >= 0; index--) {
                request.CurrentTarget = target.Route[index];
                request.CurrentTarget.RaiseMiddleClicked(request);
                if (request.Handled) {
                    break;
                }
            }
        }
    }

    private static bool IsInside(WindowHitTarget target, PointerInputEvent input) {
        return new RoutedInputEvent(input, target).IsInsideTarget;
    }
}
