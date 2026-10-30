namespace Melanopsin.Graphics.Text.Fonts;

public interface ITextMeasurer {
    TextMetrics MeasureText(string text, float fontSize);
}
