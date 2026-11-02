using System;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssColorParser {
    private static bool TryParseFunction(
        string value,
        out string? expression,
        out string? error) {
        expression = null;
        error = null;
        var open = value.IndexOf('(');
        if (open <= 0) {
            return false;
        }

        var function = value.Substring(0, open).Trim().ToLowerInvariant();
        if (function is not ("rgb" or "rgba" or "hsl" or "hsla" or "hsv" or "hsva")) {
            return false;
        }

        if (!CssLiteralParser.TryGetFunctionArguments(value, function, out var arguments)) {
            error = $"Invalid color function syntax in '{value}'.";
            return false;
        }

        if (!TrySplitArguments(arguments, out var channels, out var alpha, out error)) {
            return false;
        }

        alpha ??= 1;
        if (function is "rgb" or "rgba") {
            var red = ParseRgbChannel(channels[0]);
            var green = ParseRgbChannel(channels[1]);
            var blue = ParseRgbChannel(channels[2]);
            if (red is null || green is null || blue is null || alpha is null) {
                error = $"Invalid RGB channel in '{value}'.";
                return false;
            }

            expression = ColorExpression(
                ToChannelByte(red.Value),
                ToChannelByte(green.Value),
                ToChannelByte(blue.Value),
                ToByte(alpha.Value));
            return true;
        }

        var hue = ParseHue(channels[0]);
        var first = ParsePercentRatio(channels[1]);
        var second = ParsePercentRatio(channels[2]);
        if (hue is null || first is null || second is null || alpha is null) {
            error = $"Invalid {(function is "hsl" or "hsla" ? "HSL" : "HSV")} component in '{value}'.";
            return false;
        }

        var rgba = function is "hsl" or "hsla"
            ? FromHsl(hue.Value, first.Value, second.Value, alpha.Value)
            : FromHsv(hue.Value, first.Value, second.Value, alpha.Value);
        expression = ColorExpression(rgba.Red, rgba.Green, rgba.Blue, rgba.Alpha);
        return true;
    }
}
