using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal static class SkiaPrimitiveConversions {
    public static SKRect ToSkRect(Rect rect) {
        return new SKRect(rect.MinX, rect.MinY, rect.MaxX, rect.MaxY);
    }

    public static SKRect ToSkRect(PixelRect rect) {
        return new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    public static SKColor ToSkColor(Color color) {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
