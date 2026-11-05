using System.ComponentModel;
using System.Runtime.InteropServices;
using Melanopsin.UI.Platform.Win32.Windowing;

namespace Melanopsin.UI.Platform.Win32;

internal sealed class Win32Notifications :
    IPlatformNotifications,
    IWindowsToastNotifications,
    IDisposable {
    private const int MaximumTitleLength = 63;
    private const int MaximumMessageLength = 255;
    private const uint TrayIconId = 1;
    private readonly object _gate = new();
    private readonly Win32WindowRegistry _windows;
    private readonly Action<NotificationActivatedEventArgs> _activatedSink;
    private readonly IntPtr _icon;
    private IntPtr _registeredWindow;
    private NotificationId? _activeId;
    private bool _disposed;

    public Win32Notifications(
        Win32WindowRegistry windows,
        Action<NotificationActivatedEventArgs> activatedSink) {
        _windows = windows;
        _activatedSink = activatedSink;
        _icon = Win32Native.LoadIconW(
            IntPtr.Zero,
            new IntPtr(Win32Native.IdiApplication));
    }

    public NotificationCapabilities Capabilities => new() {
        CanSubmit = !_disposed && _icon != IntPtr.Zero,
        Features = _icon == IntPtr.Zero
            ? NotificationFeature.None
            : NotificationFeature.Body |
              NotificationFeature.Dismiss |
              NotificationFeature.Replacement,
        Limitation = "The Win32 fallback uses a tray balloon. Actions, progress, expiry, and portable icons require Windows Toast support.",
    };

    public WindowsToastCapabilities WindowsCapabilities => new() {
        CanSubmitRawXml = false,
        Limitation = "Raw Toast XML is unavailable without the Windows App SDK or WinRT projection.",
    };

    WindowsToastCapabilities IWindowsToastNotifications.Capabilities =>
        WindowsCapabilities;

    public event EventHandler<NotificationActivatedEventArgs>? Activated;

    public ValueTask<NotificationResult> ShowAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Capabilities.CanSubmit) {
            return ValueTask.FromResult(NotificationResult.Unavailable());
        }

        if (request.Actions.Count != 0) {
            return ValueTask.FromResult(
                NotificationResult.Rejected(NotificationFeature.Actions));
        }

        var omitted = NotificationFeature.None;
        if (request.Icon is not null) {
            omitted |= NotificationFeature.Icon;
        }

        if (request.Progress is not null) {
            omitted |= NotificationFeature.Progress;
        }

        if (request.ExpiresAfter is not null) {
            omitted |= NotificationFeature.Expiry;
        }

        lock (_gate) {
            var window = FindWindow();
            if (window == IntPtr.Zero) {
                return ValueTask.FromResult(NotificationResult.Unavailable());
            }

            EnsureIcon(window, request);
            _activeId = request.Id;
        }

        var applied = GetAppliedFeatures(request);
        return ValueTask.FromResult(omitted == NotificationFeature.None
            ? NotificationResult.Accepted(applied)
            : NotificationResult.Degraded(
                applied,
                omitted));
    }

    public ValueTask<NotificationResult> ShowAsync(
        WindowsToastRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateToastXml(request.Xml);
        var fallback = ShowAsync(request.Fallback, cancellationToken);
        return ShowFallbackAsync(fallback);
    }

    public ValueTask<bool> DismissAsync(
        NotificationId id,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate) {
            if (_activeId != id) {
                return ValueTask.FromResult(false);
            }

            RemoveIcon();
            _activeId = null;
            return ValueTask.FromResult(true);
        }
    }

    public bool HandleCallbackMessage(IntPtr value) {
        var message = unchecked((uint)value.ToInt64()) & 0xFFFF;
        if (message != Win32Native.NinBalloonUserClick &&
            message != Win32Native.WmLButtonUp) {
            return false;
        }

        NotificationId? id;
        lock (_gate) {
            id = _activeId;
        }

        if (id is { } activeId) {
            _activatedSink(new NotificationActivatedEventArgs {
                Id = activeId,
                Kind = NotificationActivationKind.Body,
            });
        }

        return true;
    }

    public void DispatchActivated(NotificationActivatedEventArgs notification) {
        Activated?.Invoke(this, notification);
    }

    public void Dispose() {
        lock (_gate) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            RemoveIcon();
            _activeId = null;
        }
    }

    private void EnsureIcon(IntPtr window, PortableNotificationRequest request) {
        if (_registeredWindow != window) {
            RemoveIcon();
            _registeredWindow = window;
        }

        var data = CreateData(window, request, TrayIconId);
        data.Flags = Win32Native.NifMessage |
            Win32Native.NifIcon |
            Win32Native.NifTip;
        if (!Win32Native.Shell_NotifyIconW(Win32Native.NimAdd, ref data) &&
            !Win32Native.Shell_NotifyIconW(Win32Native.NimModify, ref data)) {
            _registeredWindow = IntPtr.Zero;
            throw LastError("Shell_NotifyIconW(NIM_ADD)");
        }

        data.Flags |= Win32Native.NifInfo;
        data.InfoFlags = Win32Native.NiifInfo |
            Win32Native.NiifRespectQuietTime;
        if (!Win32Native.Shell_NotifyIconW(Win32Native.NimModify, ref data)) {
            RemoveIcon();
            throw LastError("Shell_NotifyIconW(NIF_INFO)");
        }
    }

    private static async ValueTask<NotificationResult> ShowFallbackAsync(
        ValueTask<NotificationResult> fallback) {
        var result = await fallback.ConfigureAwait(false);
        return result.Kind is NotificationResultKind.Accepted or
            NotificationResultKind.Degraded
            ? NotificationResult.Degraded(
                result.AppliedFeatures,
                result.OmittedFeatures | NotificationFeature.CustomContent)
            : result;
    }

    private static NotificationFeature GetAppliedFeatures(
        PortableNotificationRequest request) {
        var features = NotificationFeature.Dismiss |
            NotificationFeature.Replacement;
        if (request.Body is not null) {
            features |= NotificationFeature.Body;
        }

        return features;
    }

    private NotifyIconData CreateData(
        IntPtr window,
        PortableNotificationRequest request,
        uint id) {
        return new NotifyIconData {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = window,
            Id = id,
            Flags = Win32Native.NifMessage,
            CallbackMessage = Win32Native.NotificationCallbackMessage,
            Icon = _icon,
            Tip = "Melanopsin",
            Info = Truncate(request.Body ?? string.Empty, MaximumMessageLength),
            InfoTitle = Truncate(request.Title, MaximumTitleLength),
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

        var data = new NotifyIconData {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = _registeredWindow,
            Id = TrayIconId,
            Tip = string.Empty,
            Info = string.Empty,
            InfoTitle = string.Empty,
        };
        _ = Win32Native.Shell_NotifyIconW(Win32Native.NimDelete, ref data);
        _registeredWindow = IntPtr.Zero;
    }

    private static string Truncate(string value, int maximumLength) {
        if (value.Length <= maximumLength) {
            return value;
        }

        var length = char.IsHighSurrogate(value[maximumLength - 1])
            ? maximumLength - 1
            : maximumLength;
        return value[..length];
    }

    private static void ValidateToastXml(string xml) {
        if (xml.Length > 64 * 1024) {
            throw new ArgumentException(
                "Toast XML cannot exceed 64 KiB.",
                nameof(xml));
        }

        if (xml.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            xml.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException(
                "Toast XML cannot declare document types or entities.",
                nameof(xml));
        }
    }

    private static Win32Exception LastError(string operation) {
        return new Win32Exception(
            Marshal.GetLastWin32Error(),
            $"Win32 notification operation '{operation}' failed.");
    }
}
