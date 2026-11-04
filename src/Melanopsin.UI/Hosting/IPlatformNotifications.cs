namespace Melanopsin.UI.Hosting;

/// <summary>
/// Optional native notification capability exposed by a platform host.
/// </summary>
public interface IPlatformNotifications {
    bool IsSupported { get; }

    ValueTask<bool> ShowAsync(
        NotificationOptions notification,
        CancellationToken cancellationToken = default);
}
