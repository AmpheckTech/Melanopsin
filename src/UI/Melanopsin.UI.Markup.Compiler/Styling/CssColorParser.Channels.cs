using System;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssColorParser {
    private static double? ParseRgbChannel(string source) {
        var value = source.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal)) {
            return CssLiteralParser.TryParseFiniteDouble(
                value.Substring(0, value.Length - 1),
                out var percentage)
                ? Clamp(percentage, 0, 100) * 2.55
                : null;
        }

        return CssLiteralParser.TryParseFiniteDouble(value, out var number)
            ? Clamp(number, 0, 255)
            : null;
    }

    private static double? ParseHue(string source) {
        var value = source.Trim();
        var multiplier = 1d;
        foreach (var (suffix, factor) in new[] {
                     ("deg", 1d),
                     ("grad", 0.9d),
                     ("rad", 180d / Math.PI),
                     ("turn", 360d),
                 }) {
            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            value = value.Substring(0, value.Length - suffix.Length);
            multiplier = factor;
            break;
        }

        if (!CssLiteralParser.TryParseFiniteDouble(value, out var amount)) {
            return null;
        }

        var degrees = amount * multiplier;
        degrees %= 360;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    private static double? ParsePercentRatio(string source) {
        var value = source.Trim();
        if (!value.EndsWith("%", StringComparison.Ordinal) ||
            !CssLiteralParser.TryParseFiniteDouble(
                value.Substring(0, value.Length - 1),
                out var percentage)) {
            return null;
        }

        return Clamp(percentage, 0, 100) / 100;
    }
}
