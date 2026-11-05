// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public sealed class WindowOptions {
    public required string Title { get; init; }

    /// <summary>
    /// The density-independent client size. The platform converts it to
    /// physical pixels using the current display scale.
    /// </summary>
    public required PixelSize Size { get; init; }

    /// <summary>
    /// The window that owns this window. Owned windows stay above their owner
    /// and close with the complete owner tree.
    /// </summary>
    public WindowId? OwnerId { get; init; }

    /// <summary>
    /// Prevents interaction with the owner hierarchy while this window is
    /// open. A modal window must have an owner.
    /// </summary>
    public bool IsModal { get; init; }
}
