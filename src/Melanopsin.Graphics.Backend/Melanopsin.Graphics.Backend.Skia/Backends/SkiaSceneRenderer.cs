using SkiaSharp;
using static Melanopsin.Graphics.Backend.Skia.SkiaPrimitiveConversions;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaSceneRenderer : IDisposable {
    private readonly ISkiaGpuContext? _gpuContext;
    private readonly SkiaCommandRenderer _commandRenderer;
    private readonly SkiaRasterCache _rasterCache;
    private readonly SkiaRasterCacheOptions _rasterCacheOptions;
    private readonly SKPaint _clearPaint = new();
    private bool _disposed;

    public SkiaSceneRenderer(
        FontFace? primaryFont,
        SKTypeface? typeface,
        ISkiaGpuContext? gpuContext,
        SkiaRasterCacheOptions rasterCacheOptions) {
        _gpuContext = gpuContext;
        _rasterCacheOptions = rasterCacheOptions;
        _rasterCache = new SkiaRasterCache(rasterCacheOptions);
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
            case BlurLayer blur:
                DrawBlurLayer(
                    canvas,
                    blur,
                    damageBounds,
                    ref activeClip,
                    translateX,
                    translateY);
                return;
            case BackdropBlurLayer backdropBlur:
                DrawBackdropBlurLayer(
                    canvas,
                    backdropBlur,
                    damageBounds,
                    ref activeClip,
                    translateX,
                    translateY);
                return;
            default:
                throw new NotSupportedException(
                    $"Skia does not support render layer '{layer.GetType().Name}'.");
        }
    }

    private void DrawBlurLayer(
        SKCanvas canvas,
        BlurLayer layer,
        Rect? damageBounds,
        ref Rect? activeClip,
        float translateX,
        float translateY) {
        var filterBounds = layer.Bounds.Inflate(BlurExtent(layer.Radius));
        if (!IntersectsVisibleRegion(
                Translate(filterBounds, translateX, translateY),
                damageBounds,
                activeClip)) {
            return;
        }

        using var imageFilter = SKImageFilter.CreateBlur(
            layer.Radius,
            layer.Radius);
        using var paint = new SKPaint {
            ImageFilter = imageFilter,
        };
        var saveCount = canvas.SaveLayer(ToSkRect(filterBounds), paint);
        DrawLayer(
            canvas,
            layer.Child,
            damageBounds,
            ref activeClip,
            translateX,
            translateY);
        canvas.RestoreToCount(saveCount);
    }

    private void DrawBackdropBlurLayer(
        SKCanvas canvas,
        BackdropBlurLayer layer,
        Rect? damageBounds,
        ref Rect? activeClip,
        float translateX,
        float translateY) {
        var screenBounds = Translate(layer.Bounds, translateX, translateY);
        if (!IntersectsVisibleRegion(screenBounds, damageBounds, activeClip)) {
            return;
        }

        using var imageFilter = SKImageFilter.CreateBlur(
            layer.Radius,
            layer.Radius);
        var saveCount = canvas.Save();
        if (layer.CornerRadius > 0) {
            canvas.ClipRoundRect(
                new SKRoundRect(
                    ToSkRect(layer.Bounds),
                    layer.CornerRadius,
                    layer.CornerRadius),
                antialias: true);
        } else {
            canvas.ClipRect(ToSkRect(layer.Bounds));
        }

        var layerSaveCount = canvas.SaveLayer(new SKCanvasSaveLayerRec {
            Bounds = ToSkRect(layer.Bounds),
            Backdrop = imageFilter,
        });
        canvas.RestoreToCount(layerSaveCount);
        canvas.RestoreToCount(saveCount);

        DrawLayer(
            canvas,
            layer.Child,
            damageBounds,
            ref activeClip,
            translateX,
            translateY);
    }

    private static float BlurExtent(float radius) {
        // Three standard deviations retain the visible Gaussian falloff while
        // keeping the isolated layer allocation bounded.
        return radius * 3;
    }

    private static bool IntersectsVisibleRegion(
        Rect bounds,
        Rect? damageBounds,
        Rect? activeClip) {
        return (damageBounds is null || bounds.IntersectsWith(damageBounds.Value)) &&
            (activeClip is null || bounds.IntersectsWith(activeClip.Value));
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

    private static Rect ToRect(PixelRect rect) {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
