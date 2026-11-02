using System;
using System.Globalization;
using Melanopsin.UI.Markup.Compiler;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    private static bool TryParseLength(
        string value,
        out string? expression,
        out string? error) {
        expression = null;
        error = null;
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) {
            expression = "global::Melanopsin.UI.Length.Auto";
            return true;
        }

        foreach (var (suffix, factory) in LengthUnitSyntax.Units) {
            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var amountText = value.Substring(0, value.Length - suffix.Length);
            var amountIsValid = TryParseFloat(amountText, out var amount) &&
                                (factory is "Px" or "Percent"
                                    ? amount >= 0
                                    : amount > 0);
            if (amountIsValid) {
                expression = $"global::Melanopsin.UI.Length.{factory}({FloatLiteral(amount)})";
                return true;
            }

            error = $"Invalid length '{value}'.";
            return false;
        }

        if (TryParseNonNegativeFloat(value, out var pixels)) {
            expression = $"global::Melanopsin.UI.Length.Px({FloatLiteral(pixels)})";
            return true;
        }

        error = $"Invalid length '{value}'. Lengths use a supported unit such as 'su', 'ru', 'fu', 'hx', 'px', or '%'; scaled units require a positive amount, while only 'px' and '%' accept zero; multi-value shorthands are not supported.";
        return false;
    }

    private static bool TryParseDuration(
        string value,
        out string? expression,
        out string? error) {
        expression = null;
        error = null;
        if (value.EndsWith("du", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                value.Substring(0, value.Length - 2),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var steps) &&
            steps > 0) {
            expression = $"global::Melanopsin.UI.Motion.UiDuration.Steps({steps})";
            return true;
        }

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
            TryParseNonNegativeFloat(value.Substring(0, value.Length - 2), out var milliseconds)) {
            expression = $"global::Melanopsin.UI.Motion.UiDuration.Ms({FloatLiteral(milliseconds)})";
            return true;
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            TryParseNonNegativeFloat(value.Substring(0, value.Length - 1), out var seconds)) {
            expression = $"global::Melanopsin.UI.Motion.UiDuration.S({FloatLiteral(seconds)})";
            return true;
        }

        error = $"Invalid duration '{value}'. Use 'ms', 's', or a positive 'du' step count.";
        return false;
    }
}
