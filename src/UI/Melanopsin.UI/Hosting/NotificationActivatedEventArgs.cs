// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public enum NotificationActivationKind {
    Body,
    Action,
    Dismissed,
}

public sealed class NotificationActivatedEventArgs : EventArgs {
    public required NotificationId Id { get; init; }

    public required NotificationActivationKind Kind { get; init; }

    public string? ActionId { get; init; }
}
