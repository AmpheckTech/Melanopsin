namespace Melanopsin.UI.Hosting;

public sealed record NotificationOptions {
    public NotificationOptions(string title, string message) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
