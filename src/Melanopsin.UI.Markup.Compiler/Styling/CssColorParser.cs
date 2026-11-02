using System;
using System.Globalization;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssColorParser {
    public static bool TryParse(
        string value,
        out string? expression,
        out string? error) {
        expression = value.ToLowerInvariant() switch {
            "primary" => "new global::Melanopsin.Graphics.Color(103, 80, 164)",
            "surface" => "new global::Melanopsin.Graphics.Color(255, 251, 254)",
            "surface-variant" => "new global::Melanopsin.Graphics.Color(231, 224, 236)",
            "error" => "new global::Melanopsin.Graphics.Color(179, 38, 30)",
            "white" => "new global::Melanopsin.Graphics.Color(255, 255, 255)",
            "black" => "new global::Melanopsin.Graphics.Color(0, 0, 0)",
            "transparent" => "new global::Melanopsin.Graphics.Color(0, 0, 0, 0)",
            _ => null,
        };
        if (expression is not null) {
            error = null;
            return true;
        }

        if (value.Length is 4 or 5 && value[0] == '#') {
            if (TryParseNibble(value[1], out var red) &&
                TryParseNibble(value[2], out var green) &&
                TryParseNibble(value[3], out var blue)) {
                var alpha = value.Length == 5 && TryParseNibble(value[4], out var parsedAlpha)
                    ? parsedAlpha * 17
                    : byte.MaxValue;
                expression = ColorExpression(red * 17, green * 17, blue * 17, alpha);
                error = null;
                return true;
            }
        } else if (value.Length is 7 or 9 && value[0] == '#') {
            if (TryParseByte(value.Substring(1, 2), out var red) &&
                TryParseByte(value.Substring(3, 2), out var green) &&
                TryParseByte(value.Substring(5, 2), out var blue)) {
                if (value.Length == 9) {
                    if (TryParseByte(value.Substring(7, 2), out var alpha)) {
                        expression = ColorExpression(red, green, blue, alpha);
                        error = null;
                        return true;
                    }
                } else {
                    expression = ColorExpression(red, green, blue, byte.MaxValue);
                    error = null;
                    return true;
                }
            }
        }

        if (TryParseFunction(value, out expression, out error)) {
            return true;
        }

        error ??= $"Invalid color '{value}'.";
        return false;
    }

    private static bool TryParseByte(string value, out byte result) {
        return byte.TryParse(
            value,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryParseNibble(char value, out byte result) {
        return byte.TryParse(
            value.ToString(),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out result);
    }
}
