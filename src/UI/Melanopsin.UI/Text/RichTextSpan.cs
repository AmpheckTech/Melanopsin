// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

public sealed class RichTextSpan {
    public RichTextSpan(
        string text,
        Color? color = null,
        float? fontSize = null,
        FontFace? font = null) {
        ArgumentNullException.ThrowIfNull(text);
        if (fontSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        Text = text;
        Color = color;
        FontSize = fontSize;
        Font = font;
    }

    public string Text { get; }

    public Color? Color { get; }

    public float? FontSize { get; }

    public FontFace? Font { get; }
}
