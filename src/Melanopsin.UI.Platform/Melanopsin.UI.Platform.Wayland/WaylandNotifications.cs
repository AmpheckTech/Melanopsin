namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandNotifications :
    IPlatformNotifications,
    ILinuxNotifications,
    IDisposable {
    private readonly LibNotifyBackend? _desktopBus;
    private readonly DesktopPortalNotifications _portal;
    private bool _disposed;

    public WaylandNotifications(
        Action<NotificationActivatedEventArgs> activatedSink) {
        _portal = new DesktopPortalNotifications(activatedSink);
        _desktopBus = LibNotifyBackend.TryCreate(activatedSink);
    }

    public NotificationCapabilities Capabilities => CurrentCapabilities.Portable;

    LinuxNotificationCapabilities ILinuxNotifications.Capabilities =>
        CurrentCapabilities;

    public event EventHandler<NotificationActivatedEventArgs>? Activated;

    private LinuxNotificationCapabilities CurrentCapabilities {
        get {
            if (_disposed) {
                var unavailable = UnavailableCapabilities();
                return new LinuxNotificationCapabilities {
                    Portable = unavailable,
                    Portal = unavailable,
                    DesktopBus = unavailable,
                    ActiveTransport = LinuxNotificationTransport.Auto,
                };
            }

            var desktopBus = _desktopBus?.Capabilities ??
                UnavailableCapabilities();
            var transport = ResolveAutomaticTransport();
            var portable = IntersectAvailable(
                _portal.Capabilities,
                desktopBus);
            return new LinuxNotificationCapabilities {
                Portable = portable,
                Portal = _portal.Capabilities,
                DesktopBus = desktopBus,
                ActiveTransport = transport,
            };
        }
    }

    private bool PreferPortal =>
        OperatingSystem.IsLinux() &&
        (File.Exists("/.flatpak-info") ||
         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")) ||
         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP")));

    public ValueTask<NotificationResult> ShowAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        return ShowAsync(
            new LinuxNotificationRequest(request),
            cancellationToken);
    }

    public ValueTask<NotificationResult> ShowAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var transport = ResolveTransport(request.Transport);
        return transport switch {
            LinuxNotificationTransport.Portal =>
                ShowPortalAsync(request, cancellationToken),
            LinuxNotificationTransport.DesktopBus =>
                ShowDesktopBusAsync(request, cancellationToken),
            _ => ValueTask.FromResult(NotificationResult.Rejected()),
        };
    }

    public ValueTask<bool> DismissAsync(
        NotificationId id,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var portalDismissed = _portal.DismissAsync(id, cancellationToken);
        var desktopDismissed = _desktopBus?.Dismiss(id) == true;
        return CombineDismissAsync(portalDismissed, desktopDismissed);
    }

    public void Pump() {
        if (!_disposed) {
            _portal.Pump();
            _desktopBus?.Pump();
        }
    }

    public void DispatchActivated(NotificationActivatedEventArgs notification) {
        Activated?.Invoke(this, notification);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _portal.Dispose();
        _desktopBus?.Dispose();
    }

    private async ValueTask<NotificationResult> ShowPortalAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken) {
        var result = await _portal.ShowAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (result.Kind != NotificationResultKind.Unavailable ||
            request.Transport == LinuxNotificationTransport.Portal ||
            _desktopBus is null) {
            return result;
        }

        return DegradeAfterPortalFallback(
            await _desktopBus.ShowAsync(request, cancellationToken)
                .ConfigureAwait(false),
            result.OmittedFeatures);
    }

    private ValueTask<NotificationResult> ShowDesktopBusAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken) {
        return _desktopBus is null
            ? ValueTask.FromResult(NotificationResult.Unavailable())
            : _desktopBus.ShowAsync(request, cancellationToken);
    }

    private LinuxNotificationTransport ResolveTransport(
        LinuxNotificationTransport requested) {
        if (requested != LinuxNotificationTransport.Auto) {
            return requested;
        }

        return ResolveAutomaticTransport();
    }

    private LinuxNotificationTransport ResolveAutomaticTransport() {
        if (PreferPortal && _portal.Capabilities.CanSubmit) {
            return LinuxNotificationTransport.Portal;
        }

        if (_desktopBus?.Capabilities.CanSubmit == true) {
            return LinuxNotificationTransport.DesktopBus;
        }

        return _portal.Capabilities.CanSubmit
            ? LinuxNotificationTransport.Portal
            : LinuxNotificationTransport.Auto;
    }

    private static NotificationResult DegradeAfterPortalFallback(
        NotificationResult fallback,
        NotificationFeature portalOmissions) {
        if (fallback.Kind is NotificationResultKind.Unavailable or
            NotificationResultKind.Rejected) {
            return fallback;
        }

        var omitted = fallback.OmittedFeatures | portalOmissions;
        return omitted == NotificationFeature.None
            ? fallback
            : NotificationResult.Degraded(fallback.AppliedFeatures, omitted);
    }

    private static NotificationCapabilities IntersectAvailable(
        NotificationCapabilities portal,
        NotificationCapabilities desktopBus) {
        if (!portal.CanSubmit) {
            return desktopBus.CanSubmit ? desktopBus : UnavailableCapabilities();
        }

        if (!desktopBus.CanSubmit) {
            return portal;
        }

        return new NotificationCapabilities {
            CanSubmit = true,
            Features = portal.Features & desktopBus.Features,
            Limitation = "Portable requests may use either Linux transport, so only their common features are reported.",
        };
    }

    private static async ValueTask<bool> CombineDismissAsync(
        ValueTask<bool> portal,
        bool desktop) {
        return await portal.ConfigureAwait(false) || desktop;
    }

    private static NotificationCapabilities UnavailableCapabilities() => new() {
        CanSubmit = false,
        Limitation = "No supported Linux notification transport is available.",
    };
}
