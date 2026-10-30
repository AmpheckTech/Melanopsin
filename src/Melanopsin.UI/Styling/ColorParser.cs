namespace Melanopsin.UI.Styling;

internal static class ColorParser {
    public static bool TryParse(string text, out Color color) {
        color = default;
        if (text.Length != 7 || text[0] != '#') {
            return false;
        }

        if (!byte.TryParse(text.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red) ||
            !byte.TryParse(text.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
            !byte.TryParse(text.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)) {
            return false;
        }

        color = new Color(red, green, blue);
        return true;
    }
}
