namespace Melanopsin.UI.Hosting;

/// <summary>
/// Optional text clipboard capability exposed by a platform host.
/// </summary>
public interface IPlatformClipboard {
    bool IsSupported { get; }

    ValueTask SetTextAsync(
        string text,
        CancellationToken cancellationToken = default);

    ValueTask<string?> GetTextAsync(
        CancellationToken cancellationToken = default);
}
