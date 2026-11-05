// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using Melanopsin.UI.Input;
using Melanopsin.UI.Platform.Win32.Windowing;

namespace Melanopsin.UI.Platform.Win32.Input;

internal sealed class Win32PointerRouter {
    private readonly Win32WindowRegistry _windows;
    private readonly Win32WindowEventQueue _events;

    public Win32PointerRouter(
        Win32WindowRegistry windows,
        Win32WindowEventQueue events) {
        _windows = windows;
        _events = events;
    }

    public void HandleMouseMove(Win32Window window, IntPtr lParam) {
        Win32Window target;
        Point point;
        if (window.HasMouseCapture) {
            target = window;
            point = Win32MessageValue.PointFromLParam(lParam);
        } else {
            (target, point) = ResolvePointerWindow(window, lParam);
        }

        if (!target.IsTrackingMouse) {
            target.IsTrackingMouse = true;
            var track = new TrackMouseEvent {
                Size = (uint)Marshal.SizeOf<TrackMouseEvent>(),
                Flags = Win32Native.TmeLeave,
                Window = target.Handle,
            };
            Win32Native.TrackMouseEvent(ref track);
            _events.QueuePointer(target, point, PointerPhase.Enter, MouseButtonKind.None);
        }

        _events.QueuePointer(target, point, PointerPhase.Move, MouseButtonKind.None);
    }

    public void HandleMouseLeave(Win32Window window, IntPtr lParam) {
        window.IsTrackingMouse = false;
        if (!window.HasMouseCapture) {
            _events.QueuePointer(
                window,
                Win32MessageValue.PointFromLParam(lParam),
                PointerPhase.Leave,
                MouseButtonKind.None);
        }
    }

    public void HandleCaptureChanged(Win32Window window) {
        if (!window.HasMouseCapture) {
            return;
        }

        window.ClearMouseCapture();
        _events.QueuePointer(
            window,
            Win32MessageValue.PointFromLParam(IntPtr.Zero),
            PointerPhase.Cancel,
            MouseButtonKind.None);
    }

    public void HandleButtonDown(
        Win32Window source,
        IntPtr lParam,
        MouseButtonKind button) {
        var (target, point) = ResolvePointerWindow(source, lParam);
        _ = Win32Native.SetFocus(target.Handle);
        target.BeginMouseCapture(button);
        Win32Native.SetCapture(target.Handle);
        _events.QueuePointer(target, point, PointerPhase.Down, button);
    }

    public void HandleButtonUp(
        Win32Window window,
        IntPtr lParam,
        MouseButtonKind button) {
        if (window.EndMouseCapture(button)) {
            Win32Native.ReleaseCapture();
        }

        _events.QueuePointer(
            window,
            Win32MessageValue.PointFromLParam(lParam),
            PointerPhase.Up,
            button);
    }

    public void HandleWheel(
        Win32Window source,
        IntPtr lParam,
        IntPtr wParam,
        bool horizontal) {
        var target = ResolveWheelWindow(source, lParam);
        var point = Win32MessageValue.PointFromLParam(lParam);
        _ = Win32Native.ScreenToClient(target.Handle, ref point);
        _events.QueueWheel(
            target,
            point,
            unchecked((short)((long)wParam >> 16)),
            horizontal);
    }

    private (Win32Window Window, Point Point) ResolvePointerWindow(
        Win32Window source,
        IntPtr clientPoint) {
        var sourcePoint = Win32MessageValue.PointFromLParam(clientPoint);
        var screenPoint = sourcePoint;
        if (!Win32Native.ClientToScreen(source.Handle, ref screenPoint)) {
            return (source, sourcePoint);
        }

        var hitWindow = Win32Native.WindowFromPoint(screenPoint);
        if (!_windows.TryGet(hitWindow, out var target)) {
            return (source, sourcePoint);
        }

        var targetPoint = screenPoint;
        _ = Win32Native.ScreenToClient(target.Handle, ref targetPoint);
        return (target, targetPoint);
    }

    private Win32Window ResolveWheelWindow(
        Win32Window fallback,
        IntPtr screenPoint) {
        var point = Win32MessageValue.PointFromLParam(screenPoint);
        var hitWindow = Win32Native.WindowFromPoint(point);
        return _windows.TryGet(hitWindow, out var target)
            ? target
            : fallback;
    }
}
