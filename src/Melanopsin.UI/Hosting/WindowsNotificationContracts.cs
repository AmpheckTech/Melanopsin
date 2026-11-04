namespace Melanopsin.UI.Hosting;

public sealed record WindowsToastCapabilities {
    public required bool CanSubmitRawXml { get; init; }

    public string? Limitation { get; init; }
}

public sealed record WindowsToastRequest {
    public WindowsToastRequest(string xml, PortableNotificationRequest fallback) {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(fallback);
        Xml = xml;
        Fallback = fallback;
    }

    public string Xml { get; }

    public PortableNotificationRequest Fallback { get; }
}

public interface IWindowsToastNotifications {
    WindowsToastCapabilities Capabilities { get; }

    ValueTask<NotificationResult> ShowAsync(
        WindowsToastRequest request,
        CancellationToken cancellationToken = default);
}
