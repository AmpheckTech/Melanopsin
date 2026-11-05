using System.Globalization;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    private static bool TryParseNonNegativeFloat(string value, out float result) {
        return TryParseFloat(value, out result) && result >= 0;
    }

    private static bool TryParseFloat(string value, out float result) {
        return float.TryParse(
                   value.Trim(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out result) &&
               !float.IsNaN(result) &&
               !float.IsInfinity(result);
    }

    private static string FloatLiteral(float value) {
        return value.ToString("R", CultureInfo.InvariantCulture) + "f";
    }
}
