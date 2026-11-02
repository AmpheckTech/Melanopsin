using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Copies the damaged BGRA pixels from a render frame into presenter-owned
/// pixel memory while preserving the destination outside the damage region.
/// </summary>
public static class BgraFrameCopier {
    private const int BytesPerPixel = 4;

    public static void Copy(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        DamageRegion damage) {
        ArgumentNullException.ThrowIfNull(damage);

        var minimumStride = checked(damage.Size.Width * BytesPerPixel);
        ValidateBuffer(source.Length, sourceStride, damage.Size, "source", minimumStride);
        ValidateBuffer(
            destination.Length,
            destinationStride,
            damage.Size,
            "destination",
            minimumStride);

        foreach (var rectangle in damage.Rects) {
            var byteCount = rectangle.Width * BytesPerPixel;
            for (var row = rectangle.Y; row < rectangle.Bottom; row++) {
                source
                    .Slice(
                        (row * sourceStride) + (rectangle.X * BytesPerPixel),
                        byteCount)
                    .CopyTo(destination.Slice(
                        (row * destinationStride) + (rectangle.X * BytesPerPixel),
                        byteCount));
            }
        }
    }

    private static void ValidateBuffer(
        int length,
        int stride,
        PixelSize size,
        string parameterName,
        int minimumStride) {
        if (stride < minimumStride) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                stride,
                "The stride must cover the full frame width.");
        }

        if (length < checked(stride * size.Height)) {
            throw new ArgumentException(
                "The pixel buffer is smaller than stride multiplied by height.",
                parameterName);
        }
    }

}
