using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class RenderFrame {
    private ReadOnlyMemory<byte> _pixels;

    public RenderFrame(
        RenderTargetDescriptor descriptor,
        RenderPixelFormat pixelFormat,
        int stride,
        ReadOnlyMemory<byte> pixels,
        DamageRegion? damage = null,
        RenderStatistics? statistics = null) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        var minimumStride = checked(descriptor.Size.Width * 4);
        if (stride < minimumStride) {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                stride,
                "The stride must cover the full frame width.");
        }

        var requiredBytes = (long)stride * descriptor.Size.Height;
        if (pixels.Length < requiredBytes) {
            throw new ArgumentException(
                "The pixel buffer is smaller than stride multiplied by height.",
                nameof(pixels));
        }

        Descriptor = descriptor;
        PixelFormat = pixelFormat;
        Stride = stride;
        _pixels = pixels;
        Damage = damage ?? DamageRegion.Full(descriptor.Size);
        if (damage is not null && damage.Size != descriptor.Size) {
            throw new ArgumentException(
                "The damage region size does not match the frame.",
                nameof(damage));
        }

        Statistics = statistics ?? default;
    }

    public RenderFrame(
        RenderTargetDescriptor descriptor,
        RenderPixelFormat pixelFormat,
        int stride,
        IGpuFrameSurface gpuSurface,
        DamageRegion? damage = null,
        RenderStatistics? statistics = null) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        ArgumentNullException.ThrowIfNull(gpuSurface);
        if (gpuSurface.Size != descriptor.Size) {
            throw new ArgumentException(
                "The GPU surface size does not match the frame.",
                nameof(gpuSurface));
        }

        Descriptor = descriptor;
        PixelFormat = pixelFormat;
        Stride = stride;
        GpuSurface = gpuSurface;
        Damage = damage ?? DamageRegion.Full(descriptor.Size);
        if (damage is not null && damage.Size != descriptor.Size) {
            throw new ArgumentException(
                "The damage region size does not match the frame.",
                nameof(damage));
        }

        Statistics = statistics ?? default;
    }

    public RenderTargetDescriptor Descriptor { get; }

    public RenderPixelFormat PixelFormat { get; }

    public int Stride { get; }

    /// <summary>
    /// Backend-owned pixel memory. It remains valid until the same target is
    /// rendered again or disposed. Presenters must not retain it without copying.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels =>
        _pixels.IsEmpty && GpuSurface is not null
            ? _pixels = GpuSurface.ReadPixels()
            : _pixels;

    public IGpuFrameSurface? GpuSurface { get; }

    public DamageRegion Damage { get; }

    public RenderStatistics Statistics { get; }
}
