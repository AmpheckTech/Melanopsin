using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Melanopsin.UI.Animation;

/// <summary>
/// Parses easing tokens used by stylesheets and other text-based sources.
/// </summary>
public static class EasingParser {
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out Easing? easing) {
        easing = null;
        var value = text.AsSpan().Trim();
        if (value.IsEmpty) {
            return false;
        }

        if (value.Equals("ease-out", StringComparison.OrdinalIgnoreCase)) {
            easing = Easing.EaseOut;
            return true;
        }

        if (value.Equals("ease-in", StringComparison.OrdinalIgnoreCase)) {
            easing = Easing.EaseIn;
            return true;
        }

        if (value.Equals("ease-in-out", StringComparison.OrdinalIgnoreCase)) {
            easing = Easing.EaseInOut;
            return true;
        }

        if (value.Equals("linear", StringComparison.OrdinalIgnoreCase)) {
            easing = Easing.Linear;
            return true;
        }

        if (value.Equals("spring", StringComparison.OrdinalIgnoreCase)) {
            easing = Easing.Spring;
            return true;
        }

        return TryParseCubicBezier(value, out easing) ||
            TryParseSteps(value, out easing);
    }

    private static bool TryParseCubicBezier(
        ReadOnlySpan<char> value,
        [NotNullWhen(true)] out Easing? easing) {
        easing = null;
        if (!TryGetFunctionArguments(value, "cubic-bezier", out var arguments)) {
            return false;
        }

        Span<float> values = stackalloc float[4];
        var remaining = arguments;
        for (var index = 0; index < 3; index++) {
            var separator = remaining.IndexOf(',');
            if (separator < 0 ||
                !TryParseFloat(remaining[..separator], out values[index])) {
                return false;
            }

            remaining = remaining[(separator + 1)..];
        }

        if (remaining.IndexOf(',') >= 0 ||
            !TryParseFloat(remaining, out values[3]) ||
            values[0] is < 0 or > 1 ||
            values[2] is < 0 or > 1) {
            return false;
        }

        easing = Easing.CubicBezier(
            values[0],
            values[1],
            values[2],
            values[3]);
        return true;
    }

    private static bool TryParseSteps(
        ReadOnlySpan<char> value,
        [NotNullWhen(true)] out Easing? easing) {
        easing = null;
        if (!TryGetFunctionArguments(value, "steps", out var arguments)) {
            return false;
        }

        var separator = arguments.IndexOf(',');
        var stepsText = separator < 0 ? arguments : arguments[..separator];
        if (!int.TryParse(
                stepsText.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var steps) ||
            steps <= 0) {
            return false;
        }

        var position = StepPosition.JumpEnd;
        if (separator >= 0 &&
            !TryParseStepPosition(arguments[(separator + 1)..], out position)) {
            return false;
        }

        try {
            easing = Easing.Step(steps, position);
        } catch (ArgumentOutOfRangeException) {
            return false;
        }

        return true;
    }

    private static bool TryParseStepPosition(
        ReadOnlySpan<char> value,
        out StepPosition position) {
        value = value.Trim();
        if (value.IndexOf(',') >= 0) {
            position = default;
            return false;
        }

        if (value.Equals("start", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("jump-start", StringComparison.OrdinalIgnoreCase)) {
            position = StepPosition.JumpStart;
            return true;
        }

        if (value.Equals("end", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("jump-end", StringComparison.OrdinalIgnoreCase)) {
            position = StepPosition.JumpEnd;
            return true;
        }

        if (value.Equals("jump-none", StringComparison.OrdinalIgnoreCase)) {
            position = StepPosition.JumpNone;
            return true;
        }

        if (value.Equals("jump-both", StringComparison.OrdinalIgnoreCase)) {
            position = StepPosition.JumpBoth;
            return true;
        }

        position = default;
        return false;
    }

    private static bool TryGetFunctionArguments(
        ReadOnlySpan<char> value,
        ReadOnlySpan<char> functionName,
        out ReadOnlySpan<char> arguments) {
        arguments = default;
        var openingParenthesis = functionName.Length;
        if (value.Length <= openingParenthesis ||
            value[^1] != ')' ||
            value[openingParenthesis] != '(' ||
            !value[..openingParenthesis].Equals(
                functionName,
                StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        arguments = value[(openingParenthesis + 1)..^1];
        return true;
    }

    private static bool TryParseFloat(
        ReadOnlySpan<char> value,
        out float result) {
        return float.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) && float.IsFinite(result);
    }
}
