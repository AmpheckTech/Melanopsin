using Melanopsin.Graphics;
using Melanopsin.Graphics.Rendering;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class DrawEditableTextCommand : RenderCommand {
    public DrawEditableTextCommand(
        string text,
        string? placeholder,
        Rect bounds,
        Color color,
        Color placeholderColor,
        Color selectionColor,
        Color caretColor,
        float fontSize,
        int selectionStart,
        int selectionLength,
        int caretIndex,
        bool isPassword,
        bool showCaret,
        FontFace? font = null) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        Text = text;
        Placeholder = placeholder;
        Bounds = bounds;
        Color = color;
        PlaceholderColor = placeholderColor;
        SelectionColor = selectionColor;
        CaretColor = caretColor;
        FontSize = fontSize;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
        CaretIndex = caretIndex;
        IsPassword = isPassword;
        ShowCaret = showCaret;
        Font = font;
    }

    public string Text { get; }

    public string? Placeholder { get; }

    public Rect Bounds { get; }

    public Color Color { get; }

    public Color PlaceholderColor { get; }

    public Color SelectionColor { get; }

    public Color CaretColor { get; }

    public float FontSize { get; }

    public int SelectionStart { get; }

    public int SelectionLength { get; }

    public int CaretIndex { get; }

    public bool IsPassword { get; }

    public bool ShowCaret { get; }

    public FontFace? Font { get; }
}
