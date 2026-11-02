using Melanopsin.Graphics.Rendering;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaGpuFrameSurface(
    SkiaRenderTarget owner,
    SkiaGpuRenderTargetHandle target,
    PixelSize size,
    DamageRegion damage,
    AccessibilityVisualOptions visualEffects)
    : IGpuFrameSurface {
    private bool _pixelsUploaded;

    public PixelSize Size => size;

    public RenderPixelFormat PixelFormat =>
        RenderPixelFormat.Bgra8888Premultiplied;

    public bool TryPresent(IntPtr nativeWindow) {
        if (visualEffects != default && !_pixelsUploaded ||
            owner.GpuContext is not { } gpuContext) {
            return false;
        }

        gpuContext.MakeCurrent();
        gpuContext.GpuContext.Flush();
        return gpuContext.TryPresent(
            nativeWindow,
            target,
            size.Width,
            size.Height);
    }

    public bool TryPresentPixels(
        IntPtr nativeWindow,
        ReadOnlySpan<byte> pixels,
        int stride) {
        if (owner.GpuContext is not { } gpuContext ||
            size.Width > int.MaxValue / 4 ||
            stride != size.Width * 4) {
            return false;
        }

        var width = size.Width;
        var height = size.Height;
        var byteCount = (long)stride * height;
        if (pixels.Length < byteCount) {
            return false;
        }

        var copy = pixels[..(int)byteCount].ToArray();
        gpuContext.UploadTexture(
            target,
            copy,
            width,
            height);
        _pixelsUploaded = true;
        return gpuContext.TryPresent(
            nativeWindow,
            target,
            width,
            height);
    }

    public void ReleasePresentSurface(IntPtr nativeWindow) {
        owner.GpuContext?.ReleaseNativeSurface(nativeWindow);
    }

    public ReadOnlyMemory<byte> ReadPixels() {
        return owner.ReadGpuPixels(damage, visualEffects);
    }
}
