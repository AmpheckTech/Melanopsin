using System;
using System.Linq;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssColorParser {
    private static bool TrySplitArguments(
        string source,
        out string[] channels,
        out double? alpha,
        out string? error) {
        channels = [];
        alpha = null;
        error = null;
        var arguments = source.Trim();
        if (arguments.Length == 0) {
            error = "A color function requires arguments.";
            return false;
        }

        if (arguments.IndexOf(',') >= 0) {
            if (arguments.IndexOf('/') >= 0) {
                error = "Comma-separated colors use a fourth argument for alpha, not '/'.";
                return false;
            }

            var parts = arguments.Split(',');
            if (parts.Length is not (3 or 4)) {
                error = "The color function requires three color channels and an optional fourth alpha argument.";
                return false;
            }

            channels = [parts[0].Trim(), parts[1].Trim(), parts[2].Trim()];
            if (parts.Length == 4) {
                alpha = ParseAlpha(parts[3]);
                if (alpha is null) {
                    error = "Invalid alpha channel.";
                    return false;
                }
            }

            if (channels.Any(static part => part.Length == 0)) {
                error = "Color channels cannot be empty.";
                return false;
            }

            return true;
        }

        var slash = arguments.IndexOf('/');
        if (slash != arguments.LastIndexOf('/')) {
            error = "A color function can contain at most one '/' alpha separator.";
            return false;
        }

        string channelSource;
        if (slash >= 0) {
            channelSource = arguments.Substring(0, slash).Trim();
            alpha = ParseAlpha(arguments.Substring(slash + 1).Trim());
            if (alpha is null) {
                error = "Invalid alpha channel.";
                return false;
            }
        } else {
            channelSource = arguments;
        }

        channels = channelSource.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (channels.Length != 3) {
            error = "A color function requires exactly three color channels.";
            return false;
        }

        return true;
    }

    private static double? ParseAlpha(string source) {
        var value = source.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal)) {
            return CssLiteralParser.TryParseFiniteDouble(
                value.Substring(0, value.Length - 1),
                out var percentage)
                ? Clamp(percentage, 0, 100) / 100
                : null;
        }

        return CssLiteralParser.TryParseFiniteDouble(value, out var alpha)
            ? Clamp(alpha, 0, 1)
            : null;
    }
}
