// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

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

    /// <summary>
    /// Gets whether this pointer event represents the primary action. Touch
    /// input has no mouse button and is always primary.
    /// </summary>
    public bool IsPrimaryButton =>
        Device != InputDeviceKind.Mouse ||
        Button is MouseButtonKind.None or MouseButtonKind.Left;

    public bool IsSecondaryButton =>
        Device == InputDeviceKind.Mouse && Button == MouseButtonKind.Right;

    public bool IsMiddleButton =>
        Device == InputDeviceKind.Mouse && Button == MouseButtonKind.Middle;

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
