using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Melanopsin.UI.Input;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Platform.Win32.Input;

internal sealed class Win32WindowEventQueue {
    private const int MaximumImeCompositionBytes = 1024 * 1024;
    private readonly Queue<WindowEvent> _events = [];

    public void Clear() {
        _events.Clear();
    }

    public bool TryDequeue(out WindowEvent windowEvent) {
        return _events.TryDequeue(out windowEvent);
    }

    public void QueueResized(Win32Window window, PixelSize size) {
        Enqueue(new WindowEvent(window.Id, WindowEventKind.Resized, size));
    }

    public void QueueMoved(Win32Window window, int x, int y) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.Moved,
            window.Size,
            X: x,
            Y: y));
    }

    public void QueueLifecycle(Win32Window window, WindowEventKind kind) {
        Enqueue(new WindowEvent(
            window.Id,
            kind,
            window.Size,
            X: window.PositionX,
            Y: window.PositionY));
    }

    public void QueueCloseRequested(Win32Window window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.CloseRequested,
            window.Size));
    }

    public void QueueDpiChanged(Win32Window window, int dpi) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.DpiChanged,
            window.Size,
            DisplayScale: dpi / 96f));
    }

    public void QueueKeyInput(Win32Window window, IntPtr wParam, IntPtr lParam) {
        if ((lParam.ToInt64() & (1L << 30)) != 0) {
            return;
        }

        var shift = (Win32Native.GetKeyState(Win32Native.VkShift) & 0x8000) != 0;
        var control = (Win32Native.GetKeyState(Win32Native.VkControl) & 0x8000) != 0;
        var alt = (Win32Native.GetKeyState(0x12) & 0x8000) != 0;
        var meta = (Win32Native.GetKeyState(0x5B) & 0x8000) != 0 ||
            (Win32Native.GetKeyState(0x5C) & 0x8000) != 0;
        var key = Win32KeyMapper.MapKey(wParam.ToInt32());
        if (key == Key.Unknown) {
            return;
        }

        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.KeyInput,
            window.Size,
            Key: new KeyInputEvent(
                window.Id,
                key,
                KeyPhase.Down,
                shift,
                control,
                alt,
                meta,
                timestamp: Environment.TickCount64)));
    }

    public void QueueTextInput(Win32Window window, IntPtr wParam) {
        var character = (char)(wParam.ToInt64() & 0xFFFF);
        if (char.IsControl(character)) {
            return;
        }

        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.KeyInput,
            window.Size,
            Key: new KeyInputEvent(
                window.Id,
                Key.Unknown,
                KeyPhase.Down,
                shift: (Win32Native.GetKeyState(Win32Native.VkShift) & 0x8000) != 0,
                text: character.ToString(),
                timestamp: Environment.TickCount64)));
    }

    public void QueueImeComposition(Win32Window window, IntPtr lParam) {
        var flags = lParam.ToInt64();
        var result = ReadCompositionString(window.Handle, Win32Native.GcsResultStr);
        if ((flags & Win32Native.GcsResultStr) != 0 && result.Length != 0) {
            window.IsComposing = false;
            QueueComposition(window, TextCompositionPhase.Commit, result);
            return;
        }

        if ((flags & Win32Native.GcsCompStr) != 0) {
            QueueComposition(
                window,
                TextCompositionPhase.Update,
                ReadCompositionString(window.Handle, Win32Native.GcsCompStr));
        }
    }

    public void QueueComposition(
        Win32Window window,
        TextCompositionPhase phase,
        string text) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.TextComposition,
            window.Size,
            Composition: new TextCompositionEvent(
                window.Id,
                phase,
                text)));
    }

    public void QueuePointer(
        Win32Window window,
        Point point,
        PointerPhase phase,
        MouseButtonKind button) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.PointerInput,
            window.Size,
            new PointerInputEvent(
                window.Id,
                new PointerId(1),
                InputDeviceKind.Mouse,
                phase,
                point.X,
                point.Y,
                button,
                isPrimary: true,
                timestamp: Environment.TickCount64)));
    }

    public void QueueWheel(
        Win32Window window,
        Point point,
        short delta,
        bool horizontal) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.PointerInput,
            window.Size,
            new PointerInputEvent(
                window.Id,
                new PointerId(1),
                InputDeviceKind.Mouse,
                PointerPhase.Scroll,
                point.X,
                point.Y,
                wheelDelta: horizontal ? 0 : delta,
                horizontalWheelDelta: horizontal ? delta : 0,
                isPrimary: true,
                timestamp: Environment.TickCount64)));
    }

    private void Enqueue(WindowEvent windowEvent) {
        _events.Enqueue(windowEvent);
    }

    private static string ReadCompositionString(IntPtr window, uint index) {
        var context = Win32Native.ImmGetContext(window);
        if (context == IntPtr.Zero) {
            return string.Empty;
        }

        try {
            var byteCount = Win32Native.ImmGetCompositionStringW(
                context,
                index,
                IntPtr.Zero,
                0);
            if (byteCount <= 0 || byteCount > MaximumImeCompositionBytes) {
                return string.Empty;
            }

            var buffer = Marshal.AllocHGlobal(byteCount);
            try {
                var copied = Win32Native.ImmGetCompositionStringW(
                    context,
                    index,
                    buffer,
                    byteCount);
                if (copied < 0 || copied > byteCount) {
                    // The composition can change between the size query and the
                    // copy. Drop a stale snapshot instead of reading it.
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(buffer, copied / sizeof(char)) ??
                    string.Empty;
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        } finally {
            _ = Win32Native.ImmReleaseContext(window, context);
        }
    }
}
