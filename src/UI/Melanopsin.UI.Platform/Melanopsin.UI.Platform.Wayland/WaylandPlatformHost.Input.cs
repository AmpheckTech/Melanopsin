using Melanopsin.UI.Input;
using NWayland;
using NWayland.Protocols.CursorShapeV1;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

public sealed partial class WaylandPlatformHost {
    private void HandleWindowPointerEnter(
        WaylandWindow window,
        uint serial,
        double x,
        double y) {
        _pointerWindow = window;
        _pointerX = x;
        _pointerY = y;
        QueuePointer(window, PointerPhase.Enter, MouseButtonKind.None, _pointerTime);
    }

    private void HandleWindowPointerLeave(
        WaylandWindow window,
        uint serial) {
        QueuePointer(window, PointerPhase.Leave, MouseButtonKind.None, _pointerTime);
        if (ReferenceEquals(_pointerWindow, window)) {
            _pointerWindow = null;
        }
    }

    private void HandlePointerMotion(double x, double y, uint time) {
        if (_pointerWindow is not { } window) {
            return;
        }

        _pointerX = x;
        _pointerY = y;
        _pointerTime = time;
        QueuePointer(window, PointerPhase.Move, MouseButtonKind.None, time);
    }

    private void HandlePointerButton(
        uint serial,
        uint button,
        WlPointer.ButtonStateEnum state,
        uint time) {
        if (_pointerWindow is not { } window) {
            return;
        }

        var mouseButton = button switch {
            ButtonLeft => MouseButtonKind.Left,
            ButtonRight => MouseButtonKind.Right,
            ButtonMiddle => MouseButtonKind.Middle,
            _ => MouseButtonKind.None,
        };
        if (mouseButton == MouseButtonKind.None) {
            return;
        }

        _inputSerial = serial;
        _pointerTime = time;
        QueuePointer(
            window,
            state == WlPointer.ButtonStateEnum.Pressed
                ? PointerPhase.Down
                : PointerPhase.Up,
            mouseButton,
            time);
    }

    private void HandlePointerAxis(
        WlPointer.AxisEnum axis,
        double value) {
        if (axis == WlPointer.AxisEnum.HorizontalScroll) {
            _axisHorizontal += value;
        } else if (axis == WlPointer.AxisEnum.VerticalScroll) {
            _axisVertical += value;
        } else {
            return;
        }

        _axisPending = true;
    }

    private void HandlePointerAxisValue120(
        WlPointer.AxisEnum axis,
        int value120) {
        if (axis == WlPointer.AxisEnum.HorizontalScroll) {
            _axisHorizontal120 += value120;
        } else if (axis == WlPointer.AxisEnum.VerticalScroll) {
            _axisVertical120 += value120;
        } else {
            return;
        }

        _axisPending = true;
    }

    private void HandlePointerFrame() {
        if (_axisPending && _pointerWindow is { } window &&
            !IsWindowInputBlocked(window)) {
            // Wayland scroll values use the opposite sign from UI wheel deltas.
            var horizontalDelta = _axisHorizontal120 != 0
                ? -_axisHorizontal120
                : (int)MathF.Round((float)-_axisHorizontal * 8);
            var verticalDelta = _axisVertical120 != 0
                ? -_axisVertical120
                : (int)MathF.Round((float)-_axisVertical * 8);
            _events.QueueScroll(
                window,
                verticalDelta,
                horizontalDelta,
                _pointerTime);
        }

        ResetPointerFrame();
    }

    private void QueuePointer(
        WaylandWindow window,
        PointerPhase phase,
        MouseButtonKind button,
        uint time) {
        window.PointerX = (float)_pointerX * window.DisplayScale;
        window.PointerY = (float)_pointerY * window.DisplayScale;
        if (IsWindowInputBlocked(window) && phase != PointerPhase.Leave) {
            return;
        }

        _events.QueuePointer(window, phase, button, time);
    }

    private bool IsWindowInputBlocked(WaylandWindow window) {
        var pending = new Stack<WindowId>();
        pending.Push(window.Id);
        while (pending.TryPop(out var ownerId)) {
            foreach (var candidate in _windows.Values) {
                if (candidate.OwnerId != ownerId) {
                    continue;
                }

                if (candidate.IsModal) {
                    return true;
                }

                pending.Push(candidate.Id);
            }
        }

        return false;
    }

    private void UpdateSeatDevices(WlSeat.CapabilityEnum capabilities) {
        var wantsPointer =
            (capabilities & WlSeat.CapabilityEnum.Pointer) != 0;
        var wantsKeyboard =
            (capabilities & WlSeat.CapabilityEnum.Keyboard) != 0;

        if (wantsPointer &&
            _pointer is null &&
            _connection.Seat is { } pointerSeat) {
            var pointer = pointerSeat.GetPointer(
                new PointerListener(this),
                _connection.Queue);
            _pointer = pointer;
            _cursorShapeDevice = _connection.CursorShapeManager?.GetPointer(
                pointer,
                _cursorShapeListener,
                _connection.Queue);
        } else if (!wantsPointer) {
            ReleasePointer();
        }

        if (wantsKeyboard &&
            _keyboard is null &&
            _connection.Seat is { } keyboardSeat) {
            _keyboard = keyboardSeat.GetKeyboard(
                new KeyboardListener(this),
                _connection.Queue);
        } else if (!wantsKeyboard) {
            ReleaseKeyboard();
        }

        if (wantsKeyboard) {
            EnsureTextInput();
        } else {
            ReleaseTextInput();
        }
    }

    private void ReleaseInputDevices() {
        ReleasePointer();
        ReleaseKeyboard();
        _xkbState?.Dispose();
        _xkbState = null;
    }

    private void ReleasePointer() {
        _pointerWindow = null;
        _pointerEnterSerial = 0;
        ResetPointerFrame();
        ReleaseCursorShapeDevice();
        if (_pointer is null) {
            return;
        }

        if (_pointer.IsReleaseAvailable) {
            _pointer.Release();
        }

        _pointer.Dispose();
        _pointer = null;
    }

    private void ReleaseCursorShapeDevice() {
        if (_cursorShapeDevice is null) {
            return;
        }

        if (!_cursorShapeDevice.IsDisposed) {
            _cursorShapeDevice.Destroy();
        }

        _cursorShapeDevice.Dispose();
        _cursorShapeDevice = null;
    }

    private void ApplyCursorShape() {
        if (_cursorShapeDevice is null || _pointerEnterSerial == 0) {
            return;
        }

        _cursorShapeDevice.SetShape(
            _pointerEnterSerial,
            MapCursorShape(_cursor));
        _connection.FlushChecked();
    }

    private static WpCursorShapeDeviceV1.ShapeEnum MapCursorShape(
        CursorKind cursor) {
        return cursor switch {
            CursorKind.ContextMenu => WpCursorShapeDeviceV1.ShapeEnum.ContextMenu,
            CursorKind.Help => WpCursorShapeDeviceV1.ShapeEnum.Help,
            CursorKind.Pointer => WpCursorShapeDeviceV1.ShapeEnum.Pointer,
            CursorKind.Progress => WpCursorShapeDeviceV1.ShapeEnum.Progress,
            CursorKind.Wait => WpCursorShapeDeviceV1.ShapeEnum.Wait,
            CursorKind.Cell => WpCursorShapeDeviceV1.ShapeEnum.Cell,
            CursorKind.Crosshair => WpCursorShapeDeviceV1.ShapeEnum.Crosshair,
            CursorKind.Text => WpCursorShapeDeviceV1.ShapeEnum.Text,
            CursorKind.VerticalText => WpCursorShapeDeviceV1.ShapeEnum.VerticalText,
            CursorKind.Alias => WpCursorShapeDeviceV1.ShapeEnum.Alias,
            CursorKind.Copy => WpCursorShapeDeviceV1.ShapeEnum.Copy,
            CursorKind.Move => WpCursorShapeDeviceV1.ShapeEnum.Move,
            CursorKind.NoDrop => WpCursorShapeDeviceV1.ShapeEnum.NoDrop,
            CursorKind.NotAllowed => WpCursorShapeDeviceV1.ShapeEnum.NotAllowed,
            CursorKind.Grab => WpCursorShapeDeviceV1.ShapeEnum.Grab,
            CursorKind.Grabbing => WpCursorShapeDeviceV1.ShapeEnum.Grabbing,
            CursorKind.EResize => WpCursorShapeDeviceV1.ShapeEnum.EResize,
            CursorKind.NResize => WpCursorShapeDeviceV1.ShapeEnum.NResize,
            CursorKind.NeResize => WpCursorShapeDeviceV1.ShapeEnum.NeResize,
            CursorKind.NwResize => WpCursorShapeDeviceV1.ShapeEnum.NwResize,
            CursorKind.SResize => WpCursorShapeDeviceV1.ShapeEnum.SResize,
            CursorKind.SeResize => WpCursorShapeDeviceV1.ShapeEnum.SeResize,
            CursorKind.SwResize => WpCursorShapeDeviceV1.ShapeEnum.SwResize,
            CursorKind.WResize => WpCursorShapeDeviceV1.ShapeEnum.WResize,
            CursorKind.EwResize => WpCursorShapeDeviceV1.ShapeEnum.EwResize,
            CursorKind.NsResize => WpCursorShapeDeviceV1.ShapeEnum.NsResize,
            CursorKind.NeswResize => WpCursorShapeDeviceV1.ShapeEnum.NeswResize,
            CursorKind.NwseResize => WpCursorShapeDeviceV1.ShapeEnum.NwseResize,
            CursorKind.ColResize => WpCursorShapeDeviceV1.ShapeEnum.ColResize,
            CursorKind.RowResize => WpCursorShapeDeviceV1.ShapeEnum.RowResize,
            CursorKind.AllScroll => WpCursorShapeDeviceV1.ShapeEnum.AllScroll,
            CursorKind.ZoomIn => WpCursorShapeDeviceV1.ShapeEnum.ZoomIn,
            CursorKind.ZoomOut => WpCursorShapeDeviceV1.ShapeEnum.ZoomOut,
            CursorKind.DndAsk => WpCursorShapeDeviceV1.ShapeEnum.DndAsk,
            CursorKind.AllResize => WpCursorShapeDeviceV1.ShapeEnum.AllResize,
            _ => WpCursorShapeDeviceV1.ShapeEnum.Default,
        };
    }

    private void ResetPointerFrame() {
        _axisPending = false;
        _axisHorizontal = 0;
        _axisVertical = 0;
        _axisHorizontal120 = 0;
        _axisVertical120 = 0;
    }

    private WaylandWindow? FindWindow(WlSurface? surface) {
        return surface is not null &&
            _windowsByHandle.TryGetValue(surface.Handle, out var window)
            ? window
            : null;
    }

    private sealed class PointerListener : WlPointer.Listener {
        private readonly WaylandPlatformHost _host;

        public PointerListener(WaylandPlatformHost host) {
            _host = host;
        }

        protected override void Enter(
            WlPointer eventSender,
            uint serial,
            WlSurface? surface,
            WlFixed surfaceX,
            WlFixed surfaceY) {
            _host._pointerEnterSerial = serial;
            if (_host.FindWindow(surface) is { } window) {
                _host.HandleWindowPointerEnter(
                    window,
                    serial,
                    (double)surfaceX,
                    (double)surfaceY);
            }
        }

        protected override void Leave(
            WlPointer eventSender,
            uint serial,
            WlSurface? surface) {
            if (_host.FindWindow(surface) is { } window) {
                _host.HandleWindowPointerLeave(window, serial);
            }
        }

        protected override void Motion(
            WlPointer eventSender,
            uint time,
            WlFixed surfaceX,
            WlFixed surfaceY) {
            _host.HandlePointerMotion(
                (double)surfaceX,
                (double)surfaceY,
                time);
        }

        protected override void Button(
            WlPointer eventSender,
            uint serial,
            uint time,
            uint button,
            WlPointer.ButtonStateEnum state) {
            _host.HandlePointerButton(serial, button, state, time);
        }

        protected override void Axis(
            WlPointer eventSender,
            uint time,
            WlPointer.AxisEnum axis,
            WlFixed value) {
            _host._pointerTime = time;
            _host.HandlePointerAxis(axis, (double)value);
        }

        protected override void Frame(WlPointer eventSender) {
            _host.HandlePointerFrame();
        }

        protected override void AxisValue120(
            WlPointer eventSender,
            WlPointer.AxisEnum axis,
            int value120) {
            _host.HandlePointerAxisValue120(axis, value120);
        }
    }

    private sealed class CursorShapeListener : WpCursorShapeDeviceV1.Listener {
    }
}
