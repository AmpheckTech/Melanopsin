// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class ShapedText {
    public ShapedText(
        string text,
        IReadOnlyList<TextLine> lines,
        float width,
        float height,
        float ascent,
        float descent) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        Text = text;
        Lines = lines;
        Width = width;
        Height = height;
        Ascent = ascent;
        Descent = descent;
    }

    public string Text { get; }

    public IReadOnlyList<TextLine> Lines { get; }

    public float Width { get; }

    public float Height { get; }

    public float Ascent { get; }

    public float Descent { get; }

    public TextMetrics ToMetrics() {
        return new TextMetrics(Width, Height, Ascent, Descent);
    }
}
