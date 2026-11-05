// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.Graphics.Rendering;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class DrawTextCommand : RenderCommand {
    public DrawTextCommand(
        string text,
        Rect bounds,
        Color color,
        float fontSize,
        TextHorizontalAlignment horizontalAlignment = TextHorizontalAlignment.Start,
        TextVerticalAlignment verticalAlignment = TextVerticalAlignment.Center,
        FontFace? font = null) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        Text = text;
        Bounds = bounds;
        Color = color;
        FontSize = fontSize;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        Font = font;
    }

    public string Text { get; }

    public Rect Bounds { get; }

    public Color Color { get; }

    public float FontSize { get; }

    public TextHorizontalAlignment HorizontalAlignment { get; }

    public TextVerticalAlignment VerticalAlignment { get; }

    public FontFace? Font { get; }
}
