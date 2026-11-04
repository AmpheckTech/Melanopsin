using System.ComponentModel;
using System.Runtime.InteropServices;
using Melanopsin.UI.Platform.Win32.Windowing;

namespace Melanopsin.UI.Platform.Win32;

internal sealed class Win32Notifications : IPlatformNotifications {
    private const uint NotificationId = 1;
    private readonly Win32WindowRegistry _windows;
    private readonly IntPtr _icon;
    private IntPtr _registeredWindow;

    public Win32Notifications(Win32WindowRegistry windows) {
        _windows = windows;
        _icon = Win32Native.LoadIconW(
            IntPtr.Zero,
            new IntPtr(Win32Native.IdiApplication));
    }

    public bool IsSupported => OperatingSystem.IsWindows() && _icon != IntPtr.Zero;

    public ValueTask<bool> ShowAsync(
        NotificationOptions notification,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        var window = FindWindow();
        if (window == IntPtr.Zero) {
            return ValueTask.FromResult(false);
        }

        if (_registeredWindow != window) {
            RemoveIcon();
            _registeredWindow = window;
        }

        var data = CreateData(window, notification);
        data.Flags = Win32Native.NifMessage |
            Win32Native.NifIcon |
            Win32Native.NifTip;
        if (!Win32Native.Shell_NotifyIconW(Win32Native.NimAdd, ref data) &&
            !Win32Native.Shell_NotifyIconW(Win32Native.NimModify, ref data)) {
            throw LastError("Shell_NotifyIconW(NIM_ADD)");
        }

        data.Flags |= Win32Native.NifInfo;
        data.InfoFlags = Win32Native.NiifInfo |
            Win32Native.NiifRespectQuietTime;
        if (!Win32Native.Shell_NotifyIconW(Win32Native.NimModify, ref data)) {
            RemoveIcon();
            throw LastError("Shell_NotifyIconW(NIF_INFO)");
        }

        return ValueTask.FromResult(true);
    }

    public void Dispose() {
        RemoveIcon();
    }

    private NotifyIconData CreateData(
        IntPtr window,
        NotificationOptions notification) {
        return new NotifyIconData {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = window,
            Id = NotificationId,
            Icon = _icon,
            Tip = "Melanopsin",
            Info = notification.Message,
            InfoTitle = notification.Title,
        };
    }

    private IntPtr FindWindow() {
        foreach (var window in _windows.Windows) {
            return window.Handle;
        }

        return IntPtr.Zero;
    }

    private void RemoveIcon() {
        if (_registeredWindow == IntPtr.Zero) {
            return;
        }

        var data = CreateData(_registeredWindow, new NotificationOptions("Melanopsin", ""));
        _ = Win32Native.Shell_NotifyIconW(Win32Native.NimDelete, ref data);
        _registeredWindow = IntPtr.Zero;
    }

    private static Win32Exception LastError(string operation) {
        return new Win32Exception(
            Marshal.GetLastWin32Error(),
            $"Win32 notification operation '{operation}' failed.");
    }
}
