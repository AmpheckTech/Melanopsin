using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public readonly record struct TextLayoutOptions {
    public TextLayoutOptions(
        float fontSize,
        float maxWidth = float.PositiveInfinity,
        TextDirection direction = TextDirection.Auto,
        FontFace? font = null) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        if (float.IsNaN(maxWidth) || maxWidth <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxWidth));
        }

        FontSize = fontSize;
        MaxWidth = maxWidth;
        Direction = direction;
        Font = font;
    }

    public float FontSize { get; }

    public float MaxWidth { get; }

    public TextDirection Direction { get; }

    public FontFace? Font { get; }
}
