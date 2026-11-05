// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Backend.Skia;

internal static class SkiaPixelEffects {
    public static void Apply(
        Span<byte> pixels,
        int stride,
        int width,
        int height,
        AccessibilityVisualOptions options,
        int startX = 0,
        int startY = 0) {
        for (var y = startY; y < startY + height; y++) {
            var row = y * stride;
            for (var x = startX; x < startX + width; x++) {
                var offset = row + (x * 4);
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var alpha = pixels[offset + 3];
                if (alpha == 0) {
                    continue;
                }

                if (alpha != 255) {
                    red = Unpremultiply(red, alpha);
                    green = Unpremultiply(green, alpha);
                    blue = Unpremultiply(blue, alpha);
                }

                var (nextRed, nextGreen, nextBlue) = options.ColorVision switch {
                    ColorVisionMode.Protanopia => Transform(
                        red, green, blue,
                        0.567, 0.433, 0.0,
                        0.558, 0.442, 0.0,
                        0.0, 0.242, 0.758),
                    ColorVisionMode.Deuteranopia => Transform(
                        red, green, blue,
                        0.625, 0.375, 0.0,
                        0.700, 0.300, 0.0,
                        0.0, 0.300, 0.700),
                    ColorVisionMode.Tritanopia => Transform(
                        red, green, blue,
                        0.950, 0.050, 0.0,
                        0.0, 0.433, 0.567,
                        0.0, 0.475, 0.525),
                    ColorVisionMode.Achromatopsia => Achromatic(red, green, blue),
                    _ => (red, green, blue),
                };
                if (options.HighContrast) {
                    nextRed = Contrast(nextRed);
                    nextGreen = Contrast(nextGreen);
                    nextBlue = Contrast(nextBlue);
                }

                if (alpha != 255) {
                    nextRed = Premultiply(nextRed, alpha);
                    nextGreen = Premultiply(nextGreen, alpha);
                    nextBlue = Premultiply(nextBlue, alpha);
                }

                pixels[offset] = nextBlue;
                pixels[offset + 1] = nextGreen;
                pixels[offset + 2] = nextRed;
            }
        }
    }

    private static (byte Red, byte Green, byte Blue) Transform(
        byte red,
        byte green,
        byte blue,
        double rr,
        double rg,
        double rb,
        double gr,
        double gg,
        double gb,
        double br,
        double bg,
        double bb) {
        return (
            ClampByte((red * rr) + (green * rg) + (blue * rb)),
            ClampByte((red * gr) + (green * gg) + (blue * gb)),
            ClampByte((red * br) + (green * bg) + (blue * bb)));
    }

    private static (byte Red, byte Green, byte Blue) Achromatic(
        byte red,
        byte green,
        byte blue) {
        var luminance = (byte)Math.Clamp(
            (red * 0.2126) + (green * 0.7152) + (blue * 0.0722),
            0,
            255);
        return (luminance, luminance, luminance);
    }

    private static byte Contrast(byte value) {
        return ClampByte(((value / 255d) - 0.5) * 1.35 * 255 + 127.5);
    }

    private static byte Unpremultiply(byte value, byte alpha) {
        return ClampByte((value * 255d) / alpha);
    }

    private static byte Premultiply(byte value, byte alpha) {
        return ClampByte((value * alpha) / 255d);
    }

    private static byte ClampByte(double value) {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
