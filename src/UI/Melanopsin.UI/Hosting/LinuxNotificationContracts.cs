namespace Melanopsin.UI.Hosting;

public enum LinuxNotificationTransport {
    Auto,
    Portal,
    DesktopBus,
}

public enum LinuxNotificationUrgency {
    Low,
    Normal,
    Critical,
}

public sealed record LinuxNotificationPresentation {
    public LinuxNotificationUrgency? Urgency { get; init; }

    public string? Category { get; init; }

    public string? DesktopEntry { get; init; }

    public string? Sound { get; init; }
}

public sealed record LinuxNotificationRequest {
    public LinuxNotificationRequest(
        PortableNotificationRequest portable,
        LinuxNotificationPresentation? presentation = null,
        LinuxNotificationTransport transport = LinuxNotificationTransport.Auto) {
        ArgumentNullException.ThrowIfNull(portable);
        if (!Enum.IsDefined(transport)) {
            throw new ArgumentOutOfRangeException(nameof(transport));
        }

        if (presentation?.Urgency is { } urgency &&
            !Enum.IsDefined(urgency)) {
            throw new ArgumentOutOfRangeException(
                nameof(presentation),
                urgency,
                "Notification urgency is not defined.");
        }

        Portable = portable;
        Presentation = presentation ?? new LinuxNotificationPresentation();
        Transport = transport;
    }

    public PortableNotificationRequest Portable { get; }

    public LinuxNotificationPresentation Presentation { get; }

    public LinuxNotificationTransport Transport { get; }
}

public sealed record LinuxNotificationCapabilities {
    public required NotificationCapabilities Portable { get; init; }

    public required NotificationCapabilities Portal { get; init; }

    public required NotificationCapabilities DesktopBus { get; init; }

    public LinuxNotificationTransport ActiveTransport { get; init; }
}

public interface ILinuxNotifications {
    LinuxNotificationCapabilities Capabilities { get; }

    ValueTask<NotificationResult> ShowAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken = default);
}
