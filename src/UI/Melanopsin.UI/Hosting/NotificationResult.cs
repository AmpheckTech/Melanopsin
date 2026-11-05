namespace Melanopsin.UI.Hosting;

public enum NotificationResultKind {
    Accepted,
    Degraded,
    Rejected,
    Unavailable,
}

public sealed record NotificationResult {
    public required NotificationResultKind Kind { get; init; }

    public required NotificationFeature AppliedFeatures { get; init; }

    public required NotificationFeature OmittedFeatures { get; init; }

    public static NotificationResult Accepted(
        NotificationFeature appliedFeatures = NotificationFeature.None) {
        return new NotificationResult {
            Kind = NotificationResultKind.Accepted,
            AppliedFeatures = appliedFeatures,
            OmittedFeatures = NotificationFeature.None,
        };
    }

    public static NotificationResult Degraded(
        NotificationFeature appliedFeatures,
        NotificationFeature omittedFeatures) {
        return new NotificationResult {
            Kind = NotificationResultKind.Degraded,
            AppliedFeatures = appliedFeatures,
            OmittedFeatures = omittedFeatures,
        };
    }

    public static NotificationResult Rejected(
        NotificationFeature omittedFeatures = NotificationFeature.None) {
        return new NotificationResult {
            Kind = NotificationResultKind.Rejected,
            AppliedFeatures = NotificationFeature.None,
            OmittedFeatures = omittedFeatures,
        };
    }

    public static NotificationResult Unavailable() {
        return new NotificationResult {
            Kind = NotificationResultKind.Unavailable,
            AppliedFeatures = NotificationFeature.None,
            OmittedFeatures = NotificationFeature.None,
        };
    }
}
