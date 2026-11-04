using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed class GestureManager {
    private const float DragThreshold = 6;
    private const float TapThreshold = 10;
    private const float SwipeThreshold = 48;
    private const long TapDuration = 300;
    private const long DoubleTapDuration = 320;
    private const long LongPressDuration = 500;

    private readonly IWindowHitTester _hitTester;
    private readonly Func<long> _timestampProvider;
    private readonly Dictionary<PointerCaptureKey, GesturePointerState> _pointers = [];
    private readonly Dictionary<(WindowId WindowId, InputDeviceKind Device), (Node? Target, long Timestamp)> _lastTaps = [];
    private readonly Dictionary<WindowId, PinchState> _pinches = [];

    public GestureManager(
        IWindowHitTester hitTester,
        Func<long>? timestampProvider = null) {
        ArgumentNullException.ThrowIfNull(hitTester);
        _hitTester = hitTester;
        _timestampProvider = timestampProvider ?? (() => Environment.TickCount64);
    }

    public event Action<GestureEvent>? Recognized;

    public void Process(PointerInputEvent input) {
        ArgumentNullException.ThrowIfNull(input);
        var key = new PointerCaptureKey(input.WindowId, input.Device, input.PointerId);
        if (input.Phase == PointerPhase.Down) {
            if (!input.IsPrimaryButton) {
                return;
            }

            _pointers[key] = new GesturePointerState(
                input,
                _hitTester.HitTest(input.WindowId, input.X, input.Y),
                _timestampProvider());
            return;
        }

        if (!_pointers.TryGetValue(key, out var pointer)) {
            return;
        }

        if (input.Phase == PointerPhase.Move) {
            pointer.LastX = input.X;
            pointer.LastY = input.Y;
            var deltaX = input.X - pointer.StartX;
            var deltaY = input.Y - pointer.StartY;
            if (MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) >= DragThreshold) {
                pointer.DragStarted = true;
                Raise(new GestureEvent(
                    GestureKind.Drag,
                    input,
                    pointer.Target?.Node,
                    deltaX,
                    deltaY));
            }

            DetectPinch(input.WindowId, input);
            return;
        }

        if (input.Phase == PointerPhase.Cancel) {
            _pointers.Remove(key);
            ResetPinchIfNeeded(input.WindowId);
            return;
        }

        if (input.Phase != PointerPhase.Up) {
            return;
        }

        var totalX = input.X - pointer.StartX;
        var totalY = input.Y - pointer.StartY;
        var distance = MathF.Sqrt((totalX * totalX) + (totalY * totalY));
        var timestamp = _timestampProvider();
        var duration = timestamp - pointer.StartTimestamp;
        if (!pointer.LongPressRaised && distance >= SwipeThreshold && duration <= 600) {
            Raise(new GestureEvent(
                GestureKind.Swipe,
                input,
                pointer.Target?.Node,
                totalX,
                totalY));
        } else if (!pointer.LongPressRaised &&
                   !pointer.DragStarted &&
                   distance <= TapThreshold &&
                   duration <= TapDuration) {
            RaiseTap(input, pointer, timestamp);
        }

        _pointers.Remove(key);
        ResetPinchIfNeeded(input.WindowId);
    }

    public void Advance(long timestamp) {
        foreach (var pair in _pointers.ToArray()) {
            var pointer = pair.Value;
            if (pointer.LongPressRaised ||
                pointer.DragStarted ||
                timestamp - pointer.StartTimestamp < LongPressDuration) {
                continue;
            }

            var deltaX = pointer.LastX - pointer.StartX;
            var deltaY = pointer.LastY - pointer.StartY;
            if (MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) > TapThreshold) {
                continue;
            }

            pointer.LongPressRaised = true;
            var source = new PointerInputEvent(
                pair.Key.WindowId,
                pair.Key.PointerId,
                pair.Key.Device,
                PointerPhase.Move,
                pointer.LastX,
                pointer.LastY,
                timestamp: timestamp);
            Raise(new GestureEvent(
                GestureKind.LongPress,
                source,
                pointer.Target?.Node));
        }
    }

    public void Advance() {
        Advance(_timestampProvider());
    }

    public void CancelWindow(WindowId windowId) {
        var keys = _pointers.Keys
            .Where(key => key.WindowId == windowId)
            .ToArray();
        foreach (var key in keys) {
            _pointers.Remove(key);
        }

        foreach (var key in _lastTaps.Keys
                     .Where(key => key.WindowId == windowId)
                     .ToArray()) {
            _lastTaps.Remove(key);
        }

        _pinches.Remove(windowId);
    }

    private void RaiseTap(
        PointerInputEvent input,
        GesturePointerState pointer,
        long timestamp) {
        var tapKey = (input.WindowId, input.Device);
        if (_lastTaps.TryGetValue(tapKey, out var previous) &&
            previous.Target == pointer.Target?.Node &&
            timestamp - previous.Timestamp <= DoubleTapDuration) {
            Raise(new GestureEvent(
                GestureKind.DoubleTap,
                input,
                pointer.Target?.Node));
            _lastTaps.Remove(tapKey);
            return;
        }

        Raise(new GestureEvent(
            GestureKind.Tap,
            input,
            pointer.Target?.Node));
        _lastTaps[tapKey] = (pointer.Target?.Node, timestamp);
    }

    private void DetectPinch(WindowId windowId, PointerInputEvent input) {
        var touches = _pointers
            .Where(pair =>
                pair.Key.WindowId == windowId &&
                pair.Key.Device == InputDeviceKind.Touch)
            .Take(2)
            .ToArray();
        if (touches.Length != 2) {
            _pinches.Remove(windowId);
            return;
        }

        var first = touches[0].Value;
        var second = touches[1].Value;
        var deltaX = second.LastX - first.LastX;
        var deltaY = second.LastY - first.LastY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var angle = Math.Atan2(deltaY, deltaX) * (180 / Math.PI);
        if (!_pinches.TryGetValue(windowId, out var pinch)) {
            pinch = new PinchState();
            _pinches.Add(windowId, pinch);
        }

        if (pinch.PreviousDistance is { } previousDistance &&
            previousDistance > 0 &&
            Math.Abs(distance - previousDistance) > 0.5f) {
            Raise(new GestureEvent(
                GestureKind.Pinch,
                input,
                first.Target?.Node,
                deltaX,
                deltaY,
                distance / previousDistance,
                (float)(angle - (pinch.PreviousAngle ?? angle))));
        }

        pinch.PreviousDistance = distance;
        pinch.PreviousAngle = angle;
    }

    private void Raise(GestureEvent gesture) {
        Recognized?.Invoke(gesture);
    }

    private void ResetPinchIfNeeded(WindowId windowId) {
        if (_pointers.Keys.Count(key =>
                key.WindowId == windowId &&
                key.Device == InputDeviceKind.Touch) < 2) {
            _pinches.Remove(windowId);
        }
    }

    private sealed class PinchState {
        public float? PreviousDistance { get; set; }

        public double? PreviousAngle { get; set; }
    }
}
