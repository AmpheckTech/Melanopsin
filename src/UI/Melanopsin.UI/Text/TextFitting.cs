using System.Globalization;
using Melanopsin.Graphics.Text;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI;

internal static class TextFitting {
    private const string Ellipsis = "…";

    public static string FitSingleLine(
        ITextLayout textLayout,
        string text,
        float fontSize,
        FontFace? font,
        float maxWidth) {
        ArgumentNullException.ThrowIfNull(textLayout);
        ArgumentNullException.ThrowIfNull(text);
        maxWidth = Math.Max(1, maxWidth);
        if (text.Length == 0 || MeasureWidth(text) <= maxWidth) {
            return text;
        }

        if (MeasureWidth(Ellipsis) > maxWidth) {
            return Ellipsis;
        }

        var elementStarts = StringInfo.ParseCombiningCharacters(text);
        var low = 0;
        var high = elementStarts.Length;
        while (low < high) {
            var count = (low + high + 1) / 2;
            var end = count == elementStarts.Length
                ? text.Length
                : elementStarts[count];
            var candidate = text[..end].TrimEnd() + Ellipsis;
            if (MeasureWidth(candidate) <= maxWidth) {
                low = count;
            } else {
                high = count - 1;
            }
        }

        var fittedEnd = low == elementStarts.Length
            ? text.Length
            : elementStarts[low];
        var fittedPrefix = text[..fittedEnd].TrimEnd();
        return fittedPrefix.Length == 0
            ? Ellipsis
            : fittedPrefix + Ellipsis;

        float MeasureWidth(string value) {
            return textLayout.Layout(
                value,
                new TextLayoutOptions(
                    fontSize,
                    maxWidth: float.PositiveInfinity,
                    font: font)).Width;
        }
    }
}
