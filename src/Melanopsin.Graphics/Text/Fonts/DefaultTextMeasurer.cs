namespace Melanopsin.Graphics.Text.Fonts;

public sealed class DefaultTextMeasurer : ITextMeasurer {
    public static DefaultTextMeasurer Instance { get; } = new();

    public TextMetrics MeasureText(string text, float fontSize) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        var width = 0f;
        foreach (var rune in text.EnumerateRunes()) {
            width += rune.Value > 0x7F ? fontSize : fontSize * 0.56f;
        }

        return new TextMetrics(width, fontSize * 1.2f, fontSize, fontSize * 0.2f);
    }
}
