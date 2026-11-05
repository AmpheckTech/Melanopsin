// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using Melanopsin.UI.Input;
using Melanopsin.UI.Platform.Win32.Input;

namespace Melanopsin.UI.Platform.Win32.Windowing;

internal sealed class Win32WindowMessageRouter {
    private readonly Win32WindowRegistry _windows;
    private readonly Win32WindowEventQueue _events;
    private readonly Win32PointerRouter _pointer;
    private readonly Action<float>? _displayScaleChanged;
    private readonly Func<IntPtr, bool>? _notificationMessage;

    public Win32WindowMessageRouter(
        Win32WindowRegistry windows,
        Win32WindowEventQueue events,
        Action<float>? displayScaleChanged = null,
        Func<IntPtr, bool>? notificationMessage = null) {
        _windows = windows;
        _events = events;
        _displayScaleChanged = displayScaleChanged;
        _notificationMessage = notificationMessage;
        _pointer = new Win32PointerRouter(windows, events);
    }

    public IntPtr WindowProcedure(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam) {
        if (!_windows.TryGet(windowHandle, out var window)) {
            return Win32Native.DefWindowProcW(windowHandle, message, wParam, lParam);
        }

        switch (message) {
            case Win32Native.NotificationCallbackMessage when
                _notificationMessage?.Invoke(lParam) == true:
                return IntPtr.Zero;

            case Win32Native.WmPaint:
                var deviceContext = Win32Native.BeginPaint(windowHandle, out var paint);
                try {
                    window.Paint(deviceContext);
                } finally {
                    Win32Native.EndPaint(windowHandle, ref paint);
                }

                return IntPtr.Zero;

            case Win32Native.WmEraseBackground:
                return new IntPtr(1);

            case Win32Native.WmSize:
                var width = Win32MessageValue.LowWord(lParam);
                var height = Win32MessageValue.HighWord(lParam);
                if (width > 0 && height > 0) {
                    var size = new PixelSize(width, height);
                    if (window.Size != size) {
                        window.UpdateSize(size);
                        _events.QueueResized(window, size);
                    }
                }

                return IntPtr.Zero;

            case Win32Native.WmMove:
                var x = Win32MessageValue.LowWord(lParam);
                var y = Win32MessageValue.HighWord(lParam);
                if (window.UpdatePosition(x, y)) {
                    _events.QueueMoved(window, x, y);
                }

                return IntPtr.Zero;

            case Win32Native.WmSetFocus:
                _events.QueueLifecycle(window, WindowEventKind.Activated);
                return IntPtr.Zero;

            case Win32Native.WmKillFocus:
                _events.QueueLifecycle(window, WindowEventKind.Deactivated);
                return IntPtr.Zero;

            case Win32Native.WmShowWindow when wParam != IntPtr.Zero:
                _events.QueueLifecycle(window, WindowEventKind.Shown);
                return IntPtr.Zero;

            case Win32Native.WmSetCursor when
                Win32MessageValue.LowWord(lParam) == Win32Native.HtClient:
                _ = Win32Native.SetCursor(window.CursorHandle);
                return new IntPtr(1);

            case Win32Native.WmClose:
                _events.QueueCloseRequested(window);
                return IntPtr.Zero;

            case Win32Native.WmKeyDown:
                if (!window.IsComposing) {
                    window.IgnoreNextTextInput = false;
                }

                _events.QueueKeyInput(window, wParam, lParam);
                return IntPtr.Zero;

            case Win32Native.WmChar:
                if (window.IsComposing || window.IgnoreNextTextInput) {
                    window.IgnoreNextTextInput = false;
                    return IntPtr.Zero;
                }

                _events.QueueTextInput(window, wParam);
                return IntPtr.Zero;

            case Win32Native.WmImeChar:
                if (!window.IsComposing && !window.IgnoreNextTextInput) {
                    _events.QueueTextInput(window, wParam);
                }

                return IntPtr.Zero;

            case Win32Native.WmImeStartComposition:
                window.IsComposing = true;
                window.ImeHasResult = false;
                window.IgnoreNextTextInput = false;
                _events.QueueComposition(
                    window,
                    TextCompositionPhase.Start,
                    string.Empty);
                return Win32Native.DefWindowProcW(windowHandle, message, wParam, lParam);

            case Win32Native.WmImeComposition:
                _events.QueueImeComposition(window, lParam);
                return Win32Native.DefWindowProcW(windowHandle, message, wParam, lParam);

            case Win32Native.WmImeEndComposition:
                if (window.IsComposing) {
                    window.IsComposing = false;
                    if (window.ImeHasResult) {
                        window.ImeHasResult = false;
                        window.IgnoreNextTextInput = true;
                    } else {
                        _events.QueueComposition(
                            window,
                            TextCompositionPhase.Cancel,
                            string.Empty);
                    }
                }

                return Win32Native.DefWindowProcW(windowHandle, message, wParam, lParam);

            case Win32Native.WmMouseMove:
                _pointer.HandleMouseMove(window, lParam);
                return IntPtr.Zero;

            case Win32Native.WmMouseLeave:
                _pointer.HandleMouseLeave(window, lParam);
                return IntPtr.Zero;

            case Win32Native.WmCaptureChanged:
                _pointer.HandleCaptureChanged(window);
                return IntPtr.Zero;

            case Win32Native.WmLButtonDown:
                _pointer.HandleButtonDown(window, lParam, MouseButtonKind.Left);
                return IntPtr.Zero;

            case Win32Native.WmLButtonUp:
                _pointer.HandleButtonUp(window, lParam, MouseButtonKind.Left);
                return IntPtr.Zero;

            case Win32Native.WmMButtonDown:
                _pointer.HandleButtonDown(window, lParam, MouseButtonKind.Middle);
                return IntPtr.Zero;

            case Win32Native.WmMButtonUp:
                _pointer.HandleButtonUp(window, lParam, MouseButtonKind.Middle);
                return IntPtr.Zero;

            case Win32Native.WmRButtonDown:
                _pointer.HandleButtonDown(window, lParam, MouseButtonKind.Right);
                return IntPtr.Zero;

            case Win32Native.WmRButtonUp:
                _pointer.HandleButtonUp(window, lParam, MouseButtonKind.Right);
                return IntPtr.Zero;

            case Win32Native.WmMouseWheel:
                _pointer.HandleWheel(window, lParam, wParam, horizontal: false);
                return IntPtr.Zero;

            case Win32Native.WmMouseHWheel:
                _pointer.HandleWheel(window, lParam, wParam, horizontal: true);
                return IntPtr.Zero;

            case Win32Native.WmDpiChanged:
                HandleDpiChanged(window, wParam, lParam);
                return IntPtr.Zero;
        }

        return Win32Native.DefWindowProcW(windowHandle, message, wParam, lParam);
    }

    private void HandleDpiChanged(
        Win32Window window,
        IntPtr wParam,
        IntPtr lParam) {
        if (lParam != IntPtr.Zero) {
            var rectangle = Marshal.PtrToStructure<Rect>(lParam);
            _ = Win32Native.SetWindowPos(
                window.Handle,
                IntPtr.Zero,
                rectangle.Left,
                rectangle.Top,
                rectangle.Right - rectangle.Left,
                rectangle.Bottom - rectangle.Top,
                Win32Native.SwpNoZOrder | Win32Native.SwpNoActivate);
            _ = window.UpdatePosition(rectangle.Left, rectangle.Top);
        }

        var dpi = Win32MessageValue.LowWord(wParam);
        if (dpi <= 0) {
            return;
        }

        var displayScale = dpi / 96f;
        _displayScaleChanged?.Invoke(displayScale);
        _events.QueueDpiChanged(window, dpi);
    }
}
