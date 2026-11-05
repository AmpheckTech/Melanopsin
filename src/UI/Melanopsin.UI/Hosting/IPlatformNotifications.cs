// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public interface IPlatformNotifications {
    NotificationCapabilities Capabilities { get; }

    ValueTask<NotificationResult> ShowAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DismissAsync(
        NotificationId id,
        CancellationToken cancellationToken = default);

    event EventHandler<NotificationActivatedEventArgs>? Activated;
}
