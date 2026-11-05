// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.UI.Hosting;

public readonly record struct WindowDescriptor {
    public WindowDescriptor(
        WindowId id,
        string title,
        PixelSize size,
        WindowId? ownerId = null,
        bool isModal = false) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (isModal && ownerId is null) {
            throw new ArgumentException(
                "A modal window must have an owner.",
                nameof(ownerId));
        }

        Id = id;
        Title = title;
        Size = size;
        OwnerId = ownerId;
        IsModal = isModal;
    }

    public WindowId Id { get; }

    public string Title { get; }

    public PixelSize Size { get; }

    public WindowId? OwnerId { get; }

    public bool IsModal { get; }
}
