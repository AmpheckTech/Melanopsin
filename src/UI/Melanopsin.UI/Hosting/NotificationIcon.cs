namespace Melanopsin.UI.Hosting;

public sealed record NotificationIcon {
    public NotificationIcon(string source) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Source = source;
    }

    public string Source { get; }
}
