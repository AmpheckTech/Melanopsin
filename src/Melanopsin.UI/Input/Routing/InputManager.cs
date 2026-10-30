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

    public bool Process(PointerInputEvent input) {
        ArgumentNullException.ThrowIfNull(input);
        var key = new PointerCaptureKey(input.WindowId, input.Device, input.PointerId);
        var hasCapture = _captures.TryGetValue(key, out var capture);
        if (!_windows.ContainsWindow(input.WindowId)) {
            return false;
        }

        if (input.Device == InputDeviceKind.Mouse &&
            input.Phase == PointerPhase.Leave &&
            !hasCapture) {
            if (_hoverTargets.Remove(key, out var leavingTarget)) {
                leavingTarget.Node.IsHovered = false;
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
                leavingTarget.Node.IsHovered = false;
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
                if (previousHover is not null) {
                    previousHover.Node.IsHovered = false;
                    dispatched |= Dispatch(
                        input.WithPhase(PointerPhase.Leave),
                        previousHover);
                }

                target.Node.IsHovered = true;
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
            dispatched |= Dispatch(input, target);
        }

        if (input.Phase == PointerPhase.Up) {
            if (input.Device == InputDeviceKind.Mouse &&
                capture is not null &&
                input.Button != MouseButtonKind.None) {
                capture.Buttons.Remove(input.Button);
                if (capture.Buttons.Count != 0) {
                    return dispatched;
                }
            }

            _captures.Remove(key);
        } else if (input.Phase == PointerPhase.Cancel) {
            if (_hoverTargets.Remove(key, out var canceledHover)) {
                canceledHover.Node.IsHovered = false;
            }

            _captures.Remove(key);
        }

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
            hoverTarget.Node.IsHovered = false;
        }
    }

    private bool Dispatch(PointerInputEvent input, WindowHitTarget target) {
        var routedEvent = new RoutedInputEvent(input, target);
        PointerRouted?.Invoke(routedEvent);
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
            if (routedEvent.Handled) {
                break;
            }
        }

        return dispatched;
    }
}
