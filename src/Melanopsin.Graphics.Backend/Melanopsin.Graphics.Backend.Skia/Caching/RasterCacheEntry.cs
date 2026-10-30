using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class RasterCacheEntry : IDisposable {
    public RasterCacheEntry(
        SKImage image,
        SKBitmap? bitmap,
        int left,
        int top,
        long bytes) {
        Image = image;
        Bitmap = bitmap;
        Left = left;
        Top = top;
        Bytes = bytes;
    }

    public SKImage Image { get; }

    public SKBitmap? Bitmap { get; }

    public int Left { get; }

    public int Top { get; }

    public long Bytes { get; }

    public long LastUsed { get; set; }

    public void Dispose() {
        Exception? failure = null;
        SkiaCleanup.Capture(ref failure, Image.Dispose);
        if (Bitmap is not null) {
            SkiaCleanup.Capture(ref failure, Bitmap.Dispose);
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }
}
