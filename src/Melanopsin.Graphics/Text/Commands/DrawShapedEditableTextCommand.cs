// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.Graphics.Rendering;

namespace Melanopsin.Graphics.Text;

public sealed class DrawShapedEditableTextCommand : RenderCommand {
    public DrawShapedEditableTextCommand(
        Rect bounds,
        ShapedText text,
        ShapedText? placeholder,
        Color color,
        Color placeholderColor,
        Color selectionColor,
        Color caretColor,
        float caretX,
        float selectionStartX,
        float selectionEndX,
        float scrollX,
        bool showCaret) {
        ArgumentNullException.ThrowIfNull(text);
        Bounds = bounds;
        Text = text;
        Placeholder = placeholder;
        Color = color;
        PlaceholderColor = placeholderColor;
        SelectionColor = selectionColor;
        CaretColor = caretColor;
        CaretX = caretX;
        SelectionStartX = selectionStartX;
        SelectionEndX = selectionEndX;
        ScrollX = scrollX;
        ShowCaret = showCaret;
    }

    public Rect Bounds { get; }

    public ShapedText Text { get; }

    public ShapedText? Placeholder { get; }

    public Color Color { get; }

    public Color PlaceholderColor { get; }

    public Color SelectionColor { get; }

    public Color CaretColor { get; }

    public float CaretX { get; }

    public float SelectionStartX { get; }

    public float SelectionEndX { get; }

    public float ScrollX { get; }

    public bool ShowCaret { get; }
}
