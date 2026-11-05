// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssColorParser {
    private static (byte Red, byte Green, byte Blue, byte Alpha) FromHsl(
        double hue,
        double saturation,
        double lightness,
        double alpha) {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var (red, green, blue) = ChromaComponents(hue, chroma);
        var match = lightness - (chroma / 2);
        return (
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match),
            ToByte(alpha));
    }

    private static (byte Red, byte Green, byte Blue, byte Alpha) FromHsv(
        double hue,
        double saturation,
        double value,
        double alpha) {
        var chroma = value * saturation;
        var (red, green, blue) = ChromaComponents(hue, chroma);
        var match = value - chroma;
        return (
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match),
            ToByte(alpha));
    }

    private static (double Red, double Green, double Blue) ChromaComponents(
        double hue,
        double chroma) {
        var sector = hue / 60;
        var intermediate = chroma * (1 - Math.Abs((sector % 2) - 1));
        return sector switch {
            < 1 => (chroma, intermediate, 0),
            < 2 => (intermediate, chroma, 0),
            < 3 => (0, chroma, intermediate),
            < 4 => (0, intermediate, chroma),
            < 5 => (intermediate, 0, chroma),
            _ => (chroma, 0, intermediate),
        };
    }

    private static byte ToByte(double value) {
        return (byte)Math.Round(
            Clamp(value, 0, 1) * byte.MaxValue,
            MidpointRounding.AwayFromZero);
    }

    private static byte ToChannelByte(double value) {
        return (byte)Math.Round(
            Clamp(value, 0, byte.MaxValue),
            MidpointRounding.AwayFromZero);
    }

    private static double Clamp(double value, double minimum, double maximum) {
        return Math.Min(maximum, Math.Max(minimum, value));
    }

    private static string ColorExpression(int red, int green, int blue, int alpha) {
        return alpha == byte.MaxValue
            ? $"new global::Melanopsin.Graphics.Color({red}, {green}, {blue})"
            : $"new global::Melanopsin.Graphics.Color({red}, {green}, {blue}, {alpha})";
    }
}
