// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class TextRun {
    public TextRun(
        FontFace font,
        TextDirection direction,
        float fontSize,
        float x,
        int start,
        int length,
        float width,
        float ascent,
        float descent,
        IReadOnlyList<TextGlyph> glyphs) {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        Font = font;
        Direction = direction;
        FontSize = fontSize;
        X = x;
        Start = start;
        Length = length;
        Width = width;
        Ascent = ascent;
        Descent = descent;
        Glyphs = glyphs;
    }

    public FontFace Font { get; }

    public TextDirection Direction { get; }

    public float FontSize { get; }

    public float X { get; }

    public int Start { get; }

    public int Length { get; }

    public float Width { get; }

    public float Ascent { get; }

    public float Descent { get; }

    public IReadOnlyList<TextGlyph> Glyphs { get; }

    public TextRun Offset(float x) {
        return new TextRun(
            Font,
            Direction,
            FontSize,
            X + x,
            Start,
            Length,
            Width,
            Ascent,
            Descent,
            Glyphs);
    }
}
