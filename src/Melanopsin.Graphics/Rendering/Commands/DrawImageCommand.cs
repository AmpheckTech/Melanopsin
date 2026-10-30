using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class DrawImageCommand : RenderCommand {
    public DrawImageCommand(
        Rect bounds,
        PixelSize size,
        RenderPixelFormat pixelFormat,
        int stride,
        ReadOnlyMemory<byte> pixels,
        float cornerRadius = 0) {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        if (stride < size.Width * 4) {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                "The stride must hold at least one four-byte pixel per column.");
        }

        if (pixels.Length < stride * size.Height) {
            throw new ArgumentException(
                "The pixel buffer is smaller than stride multiplied by height.",
                nameof(pixels));
        }

        Bounds = bounds;
        Size = size;
        PixelFormat = pixelFormat;
        Stride = stride;
        Pixels = pixels;
        CornerRadius = cornerRadius;
    }

    public Rect Bounds { get; }

    public PixelSize Size { get; }

    public RenderPixelFormat PixelFormat { get; }

    public int Stride { get; }

    public float CornerRadius { get; }

    /// <summary>
    /// Pixels remain valid until the next render of the producing object.
    /// Backends must copy them if they keep the image beyond that render.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }
}
