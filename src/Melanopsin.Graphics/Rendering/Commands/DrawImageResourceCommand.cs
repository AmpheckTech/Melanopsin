// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

public sealed class DrawImageResourceCommand : RenderCommand {
    public DrawImageResourceCommand(
        Rect bounds,
        ImageResource image,
        float cornerRadius = 0) {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        Bounds = bounds;
        Image = image;
        CornerRadius = cornerRadius;
    }

    public Rect Bounds { get; }

    public ImageResource Image { get; }

    public float CornerRadius { get; }
}
