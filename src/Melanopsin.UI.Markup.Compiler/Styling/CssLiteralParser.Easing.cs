using System;
using System.Globalization;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    private static bool TryParseEasing(
        string value,
        out string? expression,
        out string? error) {
        expression = value.ToLowerInvariant() switch {
            "ease-out" => "global::Melanopsin.UI.Animation.Easing.EaseOut",
            "ease-in" => "global::Melanopsin.UI.Animation.Easing.EaseIn",
            "ease-in-out" => "global::Melanopsin.UI.Animation.Easing.EaseInOut",
            "linear" => "global::Melanopsin.UI.Animation.Easing.Linear",
            "spring" => "global::Melanopsin.UI.Animation.Easing.Spring",
            _ => null,
        };
        if (expression is not null) {
            error = null;
            return true;
        }

        if (TryGetFunctionArguments(value, "cubic-bezier", out var arguments)) {
            var values = arguments.Split(',');
            if (values.Length == 4 &&
                TryParseFloat(values[0], out var x1) &&
                TryParseFloat(values[1], out var y1) &&
                TryParseFloat(values[2], out var x2) &&
                TryParseFloat(values[3], out var y2) &&
                x1 is >= 0 and <= 1 &&
                x2 is >= 0 and <= 1) {
                expression = "global::Melanopsin.UI.Animation.Easing.CubicBezier(" +
                             FloatLiteral(x1) + ", " + FloatLiteral(y1) + ", " +
                             FloatLiteral(x2) + ", " + FloatLiteral(y2) + ")";
                error = null;
                return true;
            }
        } else if (TryGetFunctionArguments(value, "steps", out arguments)) {
            var values = arguments.Split(',');
            if (values.Length is 1 or 2 &&
                int.TryParse(
                    values[0].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var count) &&
                count > 0) {
                var position = "JumpEnd";
                if (values.Length == 2 && !TryGetStepPosition(values[1], out position)) {
                    error = $"Invalid step position '{values[1].Trim()}'.";
                    return false;
                }

                if (position == "JumpNone" && count < 2) {
                    error = "The 'jump-none' step position requires at least two steps.";
                    return false;
                }

                expression = "global::Melanopsin.UI.Animation.Easing.Step(" +
                             count.ToString(CultureInfo.InvariantCulture) +
                             ", global::Melanopsin.UI.Animation.StepPosition." +
                             position + ")";
                error = null;
                return true;
            }
        }

        error = $"Invalid easing '{value}'.";
        return false;
    }

    private static bool TryGetStepPosition(string value, out string position) {
        position = value.Trim().ToLowerInvariant() switch {
            "start" or "jump-start" => "JumpStart",
            "end" or "jump-end" => "JumpEnd",
            "jump-none" => "JumpNone",
            "jump-both" => "JumpBoth",
            _ => string.Empty,
        };
        return position.Length != 0;
    }
}
