// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class StrokeRectangleCommand : RenderCommand {
    public StrokeRectangleCommand(
        Rect bounds,
        Color color,
        float strokeWidth,
        float cornerRadius = 0) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strokeWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        Bounds = bounds;
        Color = color;
        StrokeWidth = strokeWidth;
        CornerRadius = cornerRadius;
    }

    public Rect Bounds { get; }

    public Color Color { get; }

    public float StrokeWidth { get; }

    public float CornerRadius { get; }
}
