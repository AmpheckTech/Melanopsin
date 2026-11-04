namespace Melanopsin.UI.Hosting;

public sealed record NotificationCapabilities {
    public required bool CanSubmit { get; init; }

    public NotificationFeature Features { get; init; }

    public string? Limitation { get; init; }
}
