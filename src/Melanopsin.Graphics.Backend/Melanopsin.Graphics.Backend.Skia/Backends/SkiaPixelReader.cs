using System.Runtime.InteropServices;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaPixelReader {
    private PixelSize _size;
    private byte[] _pixels;
    private SKBitmap? _bitmap;
    private SKSurface? _gpuSurface;

    public SkiaPixelReader(PixelSize size, SKBitmap bitmap) {
        _size = size;
        _bitmap = bitmap;
        _pixels = new byte[checked(size.Width * size.Height * 4)];
    }

    public SkiaPixelReader(PixelSize size, SKSurface gpuSurface) {
        _size = size;
        _gpuSurface = gpuSurface;
        _pixels = [];
    }

    public void ResizeCpu(PixelSize size, SKBitmap bitmap) {
        EnsureCapacity(size);
        _size = size;
        _bitmap = bitmap;
        _gpuSurface = null;
    }

    public void ResizeGpu(PixelSize size, SKSurface gpuSurface) {
        _size = size;
        _bitmap = null;
        _gpuSurface = gpuSurface;
    }

    public void Read(
        DamageRegion damage,
        AccessibilityVisualOptions visualEffects) {
        EnsureCapacity(_size);
        if (_gpuSurface is not null) {
            Read(_gpuSurface, damage, visualEffects);
            return;
        }

        CopyCpuPixels(_bitmap!, damage, visualEffects);
    }

    public void Read(
        SKSurface surface,
        DamageRegion damage,
        AccessibilityVisualOptions visualEffects) {
        ArgumentNullException.ThrowIfNull(surface);
        EnsureCapacity(_size);
        CopyGpuPixels(surface, damage, visualEffects);
    }

    public void CopyTo(Span<byte> destination, int destinationStride) {
        var sourceStride = checked(_size.Width * 4);
        var required = checked(sourceStride * _size.Height);
        if (destinationStride < sourceStride ||
            destination.Length < checked(destinationStride * _size.Height)) {
            throw new ArgumentException(
                "The destination buffer cannot hold the complete frame.",
                nameof(destination));
        }

        EnsureCapacity(_size);
        for (var row = 0; row < _size.Height; row++) {
            _pixels.AsSpan(row * sourceStride, sourceStride).CopyTo(
                destination.Slice(row * destinationStride, sourceStride));
        }
    }

    private void EnsureCapacity(PixelSize size) {
        var required = checked(size.Width * size.Height * 4);
        if (_pixels.Length < required) {
            Array.Resize(ref _pixels, required);
        }
    }

    private void CopyCpuPixels(
        SKBitmap bitmap,
        DamageRegion damage,
        AccessibilityVisualOptions visualEffects) {
        var source = bitmap.GetPixels();
        var sourceStride = bitmap.RowBytes;
        var destinationStride = checked(_size.Width * 4);
        if (damage.IsFull) {
            CopyRows(
                source,
                sourceStride,
                _pixels,
                destinationStride,
                _size.Width,
                _size.Height,
                sourceX: 0,
                destinationX: 0,
                sourceY: 0);
            if (visualEffects != default) {
                SkiaPixelEffects.Apply(
                    _pixels.AsSpan(0, checked(destinationStride * _size.Height)),
                    destinationStride,
                    _size.Width,
                    _size.Height,
                    visualEffects);
            }

            return;
        }

        foreach (var rect in damage.Rects) {
            CopyRows(
                source,
                sourceStride,
                _pixels,
                destinationStride,
                rect.Width,
                rect.Height,
                sourceX: rect.X,
                destinationX: rect.X,
                sourceY: rect.Y);
            if (visualEffects != default) {
                SkiaPixelEffects.Apply(
                    _pixels.AsSpan(0, checked(destinationStride * _size.Height)),
                    destinationStride,
                    rect.Width,
                    rect.Height,
                    visualEffects,
                    rect.X,
                    rect.Y);
            }
        }
    }

    private static void CopyRows(
        IntPtr source,
        int sourceStride,
        byte[] destination,
        int destinationStride,
        int width,
        int height,
        int sourceX,
        int destinationX,
        int sourceY) {
        var byteCount = checked(width * 4);
        if (sourceStride == destinationStride &&
            sourceX == 0 &&
            destinationX == 0) {
            var offset = checked(sourceY * sourceStride);
            Marshal.Copy(source + offset, destination, offset, checked(byteCount * height));
            return;
        }

        for (var row = 0; row < height; row++) {
            var sourceOffset = checked(
                ((sourceY + row) * sourceStride) + (sourceX * 4));
            var destinationOffset = checked(
                ((sourceY + row) * destinationStride) + (destinationX * 4));
            Marshal.Copy(
                source + sourceOffset,
                destination,
                destinationOffset,
                byteCount);
        }
    }

    private void CopyGpuPixels(
        SKSurface surface,
        DamageRegion damage,
        AccessibilityVisualOptions visualEffects) {
        var stride = checked(_size.Width * 4);
        var byteCount = checked(stride * _size.Height);
        var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        try {
            var destination = handle.AddrOfPinnedObject();
            IReadOnlyList<PixelRect> rects = damage.IsFull
                ? [PixelRect.FromSize(_size)]
                : damage.Rects;
            foreach (var rect in rects) {
                var info = new SKImageInfo(
                    rect.Width,
                    rect.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);
                var offset = checked((rect.Y * stride) + (rect.X * 4));
                if (!surface.ReadPixels(
                        info,
                        IntPtr.Add(destination, offset),
                        stride,
                        rect.X,
                        rect.Y)) {
                    throw new InvalidOperationException(
                        "Skia could not read pixels from the GPU surface.");
                }

                if (visualEffects != default) {
                    SkiaPixelEffects.Apply(
                        _pixels.AsSpan(0, byteCount),
                        stride,
                        rect.Width,
                        rect.Height,
                        visualEffects,
                        rect.X,
                        rect.Y);
                }
            }
        } finally {
            handle.Free();
        }
    }
}
