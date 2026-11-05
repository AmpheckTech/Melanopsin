using System;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    private static bool TryParseBlur(
        string value,
        out string? expression,
        out string? error) {
        expression = null;
        error = null;
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            expression = "global::Melanopsin.UI.Styling.BlurFilter.None";
            return true;
        }

        if (!TryGetFunctionArguments(value, "blur", out var arguments)) {
            error = $"Invalid filter '{value}'. Use 'none' or 'blur(<length>)'.";
            return false;
        }

        var radius = arguments.Trim();
        if (radius.Length == 0 ||
            string.Equals(radius, "auto", StringComparison.OrdinalIgnoreCase) ||
            radius.EndsWith("%", StringComparison.OrdinalIgnoreCase) ||
            radius.EndsWith("fr", StringComparison.OrdinalIgnoreCase)) {
            error = $"Invalid blur radius '{arguments}'. Blur requires a non-negative resolvable length; auto, percentages, and fractions are not supported.";
            return false;
        }

        if (!TryParseLength(radius, out var length, out _)) {
            error = $"Invalid blur radius '{arguments}'. Blur requires a non-negative resolvable length; auto, percentages, and fractions are not supported.";
            return false;
        }

        expression =
            "global::Melanopsin.UI.Styling.BlurFilter.Blur(" + length + ")";
        return true;
    }
}
