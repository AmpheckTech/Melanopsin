// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public sealed record PortableNotificationRequest {
    private readonly NotificationAction[] _actions;

    public PortableNotificationRequest(
        NotificationId id,
        string title,
        string? body = null,
        NotificationIcon? icon = null,
        IEnumerable<NotificationAction>? actions = null,
        double? progress = null,
        TimeSpan? expiresAfter = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (progress is { } progressValue &&
            (!double.IsFinite(progressValue) ||
             progressValue < 0 ||
             progressValue > 1)) {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress,
                "Notification progress must be between 0 and 1.");
        }

        if (expiresAfter is { } expiry &&
            expiry < TimeSpan.Zero &&
            expiry != Timeout.InfiniteTimeSpan) {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAfter),
                expiry,
                "Notification expiry must be non-negative or infinite.");
        }

        var actionValues = actions?.ToArray() ?? [];
        if (actionValues.Any(static action => action is null)) {
            throw new ArgumentException(
                "Notification actions cannot contain null entries.",
                nameof(actions));
        }

        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actionValues) {
            if (!actionIds.Add(action.Id)) {
                throw new ArgumentException(
                    $"Notification action id '{action.Id}' is duplicated.",
                    nameof(actions));
            }
        }

        Id = id;
        Title = title;
        Body = body;
        Icon = icon;
        _actions = actionValues;
        Progress = progress;
        ExpiresAfter = expiresAfter;
    }

    public NotificationId Id { get; }

    public string Title { get; }

    public string? Body { get; }

    public NotificationIcon? Icon { get; }

    public IReadOnlyList<NotificationAction> Actions => _actions;

    public double? Progress { get; }

    public TimeSpan? ExpiresAfter { get; }
}
