namespace Melanopsin.UI;

public interface ICustomTextMeasurer {
    TextMetrics MeasureTextLayout(
        ITextLayout textLayout,
        float maxWidth,
        float defaultFontSize,
        FontFace? defaultFont = null);
}
