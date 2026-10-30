namespace Melanopsin.UI.Input;

public sealed class PointerInputEvent {
    public PointerInputEvent(
        WindowId windowId,
        PointerId pointerId,
        InputDeviceKind device,
        PointerPhase phase,
        float x,
        float y,
        MouseButtonKind button = MouseButtonKind.None,
        int wheelDelta = 0,
        int horizontalWheelDelta = 0,
        float pressure = 1,
        bool isPrimary = false,
        long timestamp = 0) {
        WindowId = windowId;
        PointerId = pointerId;
        Device = device;
        Phase = phase;
        X = x;
        Y = y;
        Button = button;
        WheelDelta = wheelDelta;
        HorizontalWheelDelta = horizontalWheelDelta;
        Pressure = pressure;
        IsPrimary = isPrimary;
        Timestamp = timestamp;
    }

    public WindowId WindowId { get; }

    public PointerId PointerId { get; }

    public InputDeviceKind Device { get; }

    public PointerPhase Phase { get; }

    public float X { get; }

    public float Y { get; }

    public MouseButtonKind Button { get; }

    public int WheelDelta { get; }

    public int HorizontalWheelDelta { get; }

    public float Pressure { get; }

    public bool IsPrimary { get; }

    public long Timestamp { get; }

    public PointerInputEvent WithPhase(PointerPhase phase) {
        return new PointerInputEvent(
            WindowId,
            PointerId,
            Device,
            phase,
            X,
            Y,
            Button,
            WheelDelta,
            HorizontalWheelDelta,
            Pressure,
            IsPrimary,
            Timestamp);
    }
}
