using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaSceneRenderer {
    private bool TryDrawRetainedLayer(
        SKCanvas canvas,
        RetainedLayer layer,
        Rect? damageBounds,
        Rect? activeClip,
        float translateX,
        float translateY) {
        if (layer.Bounds.IsEmpty) {
            return true;
        }

        var screenBounds = Translate(layer.Bounds, translateX, translateY);
        if (damageBounds is { } damage &&
            !screenBounds.IntersectsWith(damage) ||
            activeClip is { } clip &&
            !screenBounds.IntersectsWith(clip)) {
            return true;
        }

        if (!_rasterCache.TryGetOrCreate(
                layer,
                CreateRasterCacheEntry,
                out var entry)) {
            return false;
        }

        canvas.DrawImage(
            entry.Image,
            entry.Left,
            entry.Top,
            new SKSamplingOptions(SKFilterMode.Nearest),
            paint: null);
        return true;
    }

    private RasterCacheEntry? CreateRasterCacheEntry(RetainedLayer layer) {
        var left = (int)MathF.Floor(layer.Bounds.MinX);
        var top = (int)MathF.Floor(layer.Bounds.MinY);
        var right = (int)MathF.Ceiling(layer.Bounds.MaxX);
        var bottom = (int)MathF.Ceiling(layer.Bounds.MaxY);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0) {
            return null;
        }

        var byteCount = (long)width * height * 4;
        if (byteCount > _rasterCacheOptions.EntryByteLimit) {
            return null;
        }

        return _gpuContext is not null
            ? CreateGpuRasterCacheEntry(
                layer,
                left,
                top,
                width,
                height,
                byteCount)
            : CreateCpuRasterCacheEntry(
                layer,
                left,
                top,
                width,
                height,
                byteCount);
    }

    private RasterCacheEntry CreateCpuRasterCacheEntry(
        RetainedLayer layer,
        int left,
        int top,
        int width,
        int height,
        long byteCount) {
        var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        var offscreen = new SKCanvas(bitmap);
        try {
            offscreen.Clear(SKColors.Transparent);
            offscreen.Translate(-left, -top);
            var cacheBounds = new Rect(left, top, width, height);
            Rect? cacheClip = cacheBounds;
            DrawLayer(
                offscreen,
                layer.Child,
                cacheBounds,
                ref cacheClip,
                translateX: 0,
                translateY: 0);
            offscreen.Flush();
            var image = SKImage.FromBitmap(bitmap);
            return new RasterCacheEntry(
                image,
                bitmap,
                left,
                top,
                byteCount);
        } catch {
            bitmap.Dispose();
            throw;
        } finally {
            offscreen.Dispose();
        }
    }

    private RasterCacheEntry CreateGpuRasterCacheEntry(
        RetainedLayer layer,
        int left,
        int top,
        int width,
        int height,
        long byteCount) {
        _gpuContext!.MakeCurrent();
        var surface = SKSurface.Create(
            _gpuContext.GpuContext,
            budgeted: true,
            info: new SKImageInfo(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul)) ??
            throw new InvalidOperationException(
                "Skia could not create a GPU raster-cache surface.");
        var offscreen = surface.Canvas;
        try {
            offscreen.Clear(SKColors.Transparent);
            offscreen.Translate(-left, -top);
            var cacheBounds = new Rect(left, top, width, height);
            Rect? cacheClip = cacheBounds;
            DrawLayer(
                offscreen,
                layer.Child,
                cacheBounds,
                ref cacheClip,
                translateX: 0,
                translateY: 0);
            offscreen.Flush();
            _gpuContext.GpuContext.Flush();
            var image = surface.Snapshot();
            return new RasterCacheEntry(
                image,
                bitmap: null,
                left,
                top,
                byteCount);
        } finally {
            surface.Dispose();
        }
    }
}
