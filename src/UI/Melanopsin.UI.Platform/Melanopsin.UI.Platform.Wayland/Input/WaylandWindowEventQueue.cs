// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Input;

namespace Melanopsin.UI.Platform.Wayland.Input;

internal sealed class WaylandWindowEventQueue {
    private readonly object _gate = new();
    private readonly Queue<WindowEvent> _events = [];

    public void Clear() {
        lock (_gate) {
            _events.Clear();
        }
    }

    public bool TryDequeue(out WindowEvent windowEvent) {
        lock (_gate) {
            return _events.TryDequeue(out windowEvent);
        }
    }

    public void QueueNotification(NotificationActivatedEventArgs notification) {
        Enqueue(new WindowEvent(
            default,
            WindowEventKind.NotificationActivated,
            default,
            Notification: notification));
    }

    public void QueueResized(WaylandWindow window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.Resized,
            window.Size));
    }

    public void QueueDpiChanged(WaylandWindow window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.DpiChanged,
            window.Size,
            DisplayScale: window.DisplayScale));
    }

    public void QueueShown(WaylandWindow window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.Shown,
            window.Size));
    }

    public void QueueRenderRequested(WaylandWindow window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.RenderRequested,
            window.Size));
    }

    public void QueueFocus(WaylandWindow window, bool focused) {
        Enqueue(new WindowEvent(
            window.Id,
            focused ? WindowEventKind.Activated : WindowEventKind.Deactivated,
            window.Size));
    }

    public void QueueCloseRequested(WaylandWindow window) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.CloseRequested,
            window.Size));
    }

    public void QueuePointer(
        WaylandWindow window,
        PointerPhase phase,
        MouseButtonKind button,
        long timestamp) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.PointerInput,
            window.Size,
            new PointerInputEvent(
                window.Id,
                new PointerId(1),
                InputDeviceKind.Mouse,
                phase,
                window.PointerX,
                window.PointerY,
                button,
                isPrimary: true,
                timestamp: timestamp)));
    }

    public void QueueScroll(
        WaylandWindow window,
        int wheelDelta,
        int horizontalWheelDelta,
        long timestamp) {
        if (wheelDelta == 0 && horizontalWheelDelta == 0) {
            return;
        }

        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.PointerInput,
            window.Size,
            new PointerInputEvent(
                window.Id,
                new PointerId(1),
                InputDeviceKind.Mouse,
                PointerPhase.Scroll,
                window.PointerX,
                window.PointerY,
                wheelDelta: wheelDelta,
                horizontalWheelDelta: horizontalWheelDelta,
                isPrimary: true,
                timestamp: timestamp)));
    }

    public void QueueKey(
        WaylandWindow window,
        Key key,
        KeyPhase phase,
        bool shift,
        bool control,
        bool alt,
        bool meta,
        string? text,
        long timestamp) {
        text = text is null ? null : FilterControlCharacters(text);
        if (key == Key.Unknown && string.IsNullOrEmpty(text)) {
            return;
        }

        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.KeyInput,
            window.Size,
            Key: new KeyInputEvent(
                window.Id,
                key,
                phase,
                shift,
                control,
                alt,
                meta,
                string.IsNullOrEmpty(text) ? null : text,
                timestamp)));
    }

    public void QueueTextComposition(
        WaylandWindow window,
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

    public void QueueTextInput(WaylandWindow window, string text) {
        text = FilterControlCharacters(text);
        if (text.Length == 0) {
            return;
        }

        QueueTextComposition(window, TextCompositionPhase.Commit, text);
    }

    public void QueueTextDeleteSurrounding(
        WaylandWindow window,
        uint beforeUtf8Bytes,
        uint afterUtf8Bytes) {
        Enqueue(new WindowEvent(
            window.Id,
            WindowEventKind.TextDeleteSurrounding,
            window.Size,
            TextDeleteSurrounding: new TextInputDeleteSurroundingEvent(
                window.Id,
                (int)Math.Min(beforeUtf8Bytes, int.MaxValue),
                (int)Math.Min(afterUtf8Bytes, int.MaxValue))));
    }

    private void Enqueue(WindowEvent windowEvent) {
        lock (_gate) {
            _events.Enqueue(windowEvent);
        }
    }

    private static string FilterControlCharacters(string value) {
        var firstControl = -1;
        for (var index = 0; index < value.Length; index++) {
            if (char.IsControl(value[index])) {
                firstControl = index;
                break;
            }
        }

        if (firstControl < 0) {
            return value;
        }

        var filtered = new System.Text.StringBuilder(value.Length - 1);
        filtered.Append(value, 0, firstControl);
        for (var index = firstControl + 1; index < value.Length; index++) {
            if (!char.IsControl(value[index])) {
                filtered.Append(value[index]);
            }
        }

        return filtered.ToString();
    }
}
