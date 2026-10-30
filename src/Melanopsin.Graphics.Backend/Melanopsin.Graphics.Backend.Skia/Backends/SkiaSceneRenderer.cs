using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaSceneRenderer : IDisposable {
    private const long MaxRasterCacheEntryBytes = 8L * 1024 * 1024;
    private readonly ISkiaGpuContext? _gpuContext;
    private readonly SkiaCommandRenderer _commandRenderer;
    private readonly SkiaRasterCache _rasterCache = new();
    private readonly SKPaint _clearPaint = new();
    private bool _disposed;

    public SkiaSceneRenderer(
        FontFace? primaryFont,
        SKTypeface? typeface,
        ISkiaGpuContext? gpuContext) {
        _gpuContext = gpuContext;
        _commandRenderer = new SkiaCommandRenderer(primaryFont, typeface);
    }

    public int RasterCacheHits => _rasterCache.Hits;

    public int RasterCacheMisses => _rasterCache.Misses;

    public long RasterCacheBytes => _rasterCache.Bytes;

    public void Render(
        SKCanvas canvas,
        RenderScene scene,
        DamageRegion damage) {
        _rasterCache.BeginFrame();
        if (damage.IsEmpty) {
            return;
        }

        if (damage.IsFull) {
            canvas.Clear(ToSkColor(scene.ClearColor));
            Rect? activeClip = null;
            DrawLayer(
                canvas,
                scene.RootLayer,
                damageBounds: null,
                ref activeClip,
                translateX: 0,
                translateY: 0);
            return;
        }

        foreach (var damageRect in damage.Rects) {
            DrawDamageRegion(
                canvas,
                scene.RootLayer,
                scene.ClearColor,
                damageRect);
        }
    }

    public void Trim() {
        _rasterCache.Trim();
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        SkiaCleanup.Capture(ref failure, _clearPaint.Dispose);
        SkiaCleanup.Capture(ref failure, _commandRenderer.Dispose);
        SkiaCleanup.Capture(ref failure, _rasterCache.Dispose);
        SkiaCleanup.ThrowIfFailed(failure);
    }

    private void DrawDamageRegion(
        SKCanvas canvas,
        RenderLayer rootLayer,
        Color clearColor,
        PixelRect damageRect) {
        var rect = ToSkRect(damageRect);
        canvas.Save();
        canvas.ClipRect(rect);
        _clearPaint.Color = ToSkColor(clearColor);
        _clearPaint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(rect, _clearPaint);
        var damageBounds = ToRect(damageRect);
        Rect? activeClip = damageBounds;
        DrawLayer(
            canvas,
            rootLayer,
            damageBounds,
            ref activeClip,
            translateX: 0,
            translateY: 0);
        canvas.Restore();
    }

    private void DrawLayer(
        SKCanvas canvas,
        RenderLayer layer,
        Rect? damageBounds,
        ref Rect? activeClip,
        float translateX,
        float translateY) {
        switch (layer) {
            case RetainedLayer retained:
                if (TryDrawRetainedLayer(
                        canvas,
                        retained,
                        damageBounds,
                        activeClip,
                        translateX,
                        translateY)) {
                    return;
                }

                DrawLayer(
                    canvas,
                    retained.Child,
                    damageBounds,
                    ref activeClip,
                    translateX,
                    translateY);
                return;
            case ContainerLayer container:
                foreach (var child in container.Children) {
                    DrawLayer(
                        canvas,
                        child,
                        damageBounds,
                        ref activeClip,
                        translateX,
                        translateY);
                }

                return;
            case ContentLayer content:
                foreach (var command in content.Commands) {
                    if (damageBounds is not null) {
                        if (!SkiaCommandRenderer.TryGetCommandBounds(
                                command,
                                out var commandBounds)) {
                            continue;
                        }

                        var translatedBounds = Translate(
                            commandBounds,
                            translateX,
                            translateY);
                        if (!translatedBounds.IntersectsWith(damageBounds.Value) ||
                            activeClip is { } clipBounds &&
                            !translatedBounds.IntersectsWith(clipBounds)) {
                            continue;
                        }
                    }

                    _commandRenderer.Draw(canvas, command);
                }

                return;
            case ClipLayer clip:
                var previousClip = activeClip;
                var screenClip = Translate(
                    clip.Bounds,
                    translateX,
                    translateY);
                activeClip = activeClip is { } current
                    ? current.Intersect(screenClip)
                    : screenClip;
                if (!activeClip.Value.IsEmpty) {
                    canvas.Save();
                    canvas.ClipRect(ToSkRect(clip.Bounds));
                    DrawLayer(
                        canvas,
                        clip.Child,
                        damageBounds,
                        ref activeClip,
                        translateX,
                        translateY);
                    canvas.Restore();
                }

                activeClip = previousClip;
                return;
            case TransformLayer transform:
                canvas.Save();
                canvas.Translate(transform.X, transform.Y);
                DrawLayer(
                    canvas,
                    transform.Child,
                    damageBounds,
                    ref activeClip,
                    translateX + transform.X,
                    translateY + transform.Y);
                canvas.Restore();
                return;
            default:
                throw new NotSupportedException(
                    $"Skia does not support render layer '{layer.GetType().Name}'.");
        }
    }

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

        var screenBounds = Translate(
            layer.Bounds,
            translateX,
            translateY);
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
        var left = (int)MathF.Floor(layer.Bounds.X);
        var top = (int)MathF.Floor(layer.Bounds.Y);
        var right = (int)MathF.Ceiling(layer.Bounds.Right);
        var bottom = (int)MathF.Ceiling(layer.Bounds.Bottom);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0) {
            return null;
        }

        var byteCount = (long)width * height * 4;
        if (byteCount > MaxRasterCacheEntryBytes) {
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

    private static Rect Translate(Rect bounds, float x, float y) {
        return x == 0 && y == 0
            ? bounds
            : new Rect(
                bounds.X + x,
                bounds.Y + y,
                bounds.Width,
                bounds.Height);
    }

    private static SKRect ToSkRect(Rect rect) {
        return new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    private static SKRect ToSkRect(PixelRect rect) {
        return new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    private static SKColor ToSkColor(Color color) {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }

    private static Rect ToRect(PixelRect rect) {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
