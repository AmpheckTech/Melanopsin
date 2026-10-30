using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaRenderTarget :
    IResizableRenderTarget {
    private SKBitmap? _bitmap;
    private SKSurface? _gpuSurface;
    private readonly ISkiaGpuContext? _gpuContext;
    private GRBackendRenderTarget? _gpuBackendRenderTarget;
    private SkiaGpuRenderTargetHandle? _gpuTarget;
    private Exception? _pendingCleanupFailure;
    private readonly SkiaSceneRenderer _sceneRenderer;
    private readonly SkiaPixelReader _pixelReader;
    private SKCanvas _canvas;
    private bool _hasFrame;
    private bool _disposed;

    public SkiaRenderTarget(
        RenderTargetDescriptor descriptor,
        FontFace? primaryFont,
        SKTypeface? typeface) {
        Descriptor = descriptor;
        var bitmap = new SKBitmap(
            descriptor.Size.Width,
            descriptor.Size.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        _bitmap = bitmap;
        _canvas = new SKCanvas(bitmap);
        _pixelReader = new SkiaPixelReader(descriptor.Size, bitmap);
        _sceneRenderer = new SkiaSceneRenderer(
            primaryFont,
            typeface,
            gpuContext: null);
    }

    internal SkiaRenderTarget(
        RenderTargetDescriptor descriptor,
        FontFace? primaryFont,
        SKTypeface? typeface,
        ISkiaGpuContext gpuContext) {
        Descriptor = descriptor;
        _gpuContext = gpuContext;
        gpuContext.MakeCurrent();
        var target = gpuContext.CreateTextureTarget(
            descriptor.Size.Width,
            descriptor.Size.Height);
        GRBackendRenderTarget? backendRenderTarget = null;
        SKSurface? surface = null;
        try {
            backendRenderTarget = CreateGpuBackendRenderTarget(target);
            surface = CreateGpuSurface(gpuContext, backendRenderTarget);
            _gpuTarget = target;
            _gpuBackendRenderTarget = backendRenderTarget;
            _gpuSurface = surface;
            _canvas = surface.Canvas;
            _pixelReader = new SkiaPixelReader(descriptor.Size, surface);
            _sceneRenderer = new SkiaSceneRenderer(
                primaryFont,
                typeface,
                gpuContext);
        } catch {
            surface?.Dispose();
            backendRenderTarget?.Dispose();
            gpuContext.DeleteTextureTarget(target);
            throw;
        }
    }

    public RenderTargetDescriptor Descriptor { get; private set; }

    internal ISkiaGpuContext? GpuContext => _gpuContext;

    internal SkiaGpuRenderTargetHandle? GpuTarget => _gpuTarget;

    public bool TryResize(RenderTargetDescriptor descriptor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (descriptor.Size == Descriptor.Size) {
            return true;
        }

        if (_gpuSurface is not null) {
            return TryResizeGpu(descriptor);
        }

        var bitmap = _bitmap!;
        var size = descriptor.Size;
        if (size.Width > bitmap.Width || size.Height > bitmap.Height) {
            var width = Math.Max(size.Width, bitmap.Width);
            var height = Math.Max(size.Height, bitmap.Height);
            _canvas.Dispose();
            bitmap.Dispose();
            bitmap = new SKBitmap(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            _bitmap = bitmap;
            _canvas = new SKCanvas(bitmap);
        }

        Descriptor = descriptor;
        _pixelReader.ResizeCpu(size, bitmap);
        _hasFrame = false;
        return true;
    }

    private bool TryResizeGpu(RenderTargetDescriptor descriptor) {
        if (_gpuContext is not { } gpuContext ||
            _gpuTarget is not { } currentTarget ||
            _gpuBackendRenderTarget is null ||
            _gpuSurface is null) {
            return false;
        }

        var size = descriptor.Size;

        try {
            gpuContext.MakeCurrent();
            var target = gpuContext.ResizeTextureTarget(
                currentTarget,
                size.Width,
                size.Height);

            // The texture has a capacity with slack (see TextureCapacity).
            // Most interactive-resize steps therefore stay inside the same
            // storage allocation. In that case there is no need to recreate
            // the Skia surface; only the logical viewport changes. The canvas
            // is clipped to Descriptor.Size below so the extra capacity cannot
            // leak into the presented frame.
            if (CanReuseGpuSurface(currentTarget, target)) {
                _gpuTarget = target;
                _pixelReader.ResizeGpu(size, _gpuSurface);
                Descriptor = descriptor;
                _hasFrame = false;
                return true;
            }

            GRBackendRenderTarget? backendRenderTarget = null;
            SKSurface? surface = null;
            try {
                backendRenderTarget = CreateGpuBackendRenderTarget(target);
                surface = CreateGpuSurface(gpuContext, backendRenderTarget);
            } catch {
                surface?.Dispose();
                backendRenderTarget?.Dispose();
                return false;
            }

            var previousTarget = currentTarget;
            var previousBackendRenderTarget = _gpuBackendRenderTarget;
            var previousSurface = _gpuSurface;

            // Commit the new target only after its backend resources were
            // created successfully. Cleanup of the old resources is
            // best-effort: a native cleanup failure must not roll the target
            // back to a partially disposed surface. Report it from Dispose.
            _gpuTarget = target;
            _gpuBackendRenderTarget = backendRenderTarget;
            _gpuSurface = surface;
            _canvas = surface.Canvas;
            _pixelReader.ResizeGpu(size, surface);
            Descriptor = descriptor;
            _hasFrame = false;

            Exception? cleanupFailure = null;
            SkiaCleanup.Capture(ref cleanupFailure, previousSurface.Dispose);
            SkiaCleanup.Capture(
                ref cleanupFailure,
                previousBackendRenderTarget.Dispose);
            if (previousTarget.TextureId != target.TextureId ||
                previousTarget.FramebufferId != target.FramebufferId) {
                SkiaCleanup.Capture(
                    ref cleanupFailure,
                    () => gpuContext.DeleteTextureTarget(previousTarget));
            }

            if (cleanupFailure is not null) {
                _pendingCleanupFailure = _pendingCleanupFailure is null
                    ? cleanupFailure
                    : new AggregateException(
                        _pendingCleanupFailure,
                        cleanupFailure);
            }

            return true;
        } catch {
            // The replacement path in Window.Resize will dispose this target.
            return false;
        }
    }

    private bool CanReuseGpuSurface(
        SkiaGpuRenderTargetHandle currentTarget,
        SkiaGpuRenderTargetHandle target) {
        return target.TextureId == currentTarget.TextureId &&
            target.FramebufferId == currentTarget.FramebufferId &&
            target.Width == currentTarget.Width &&
            target.Height == currentTarget.Height &&
            _gpuBackendRenderTarget is { } backendRenderTarget &&
            backendRenderTarget.Width == target.Width &&
            backendRenderTarget.Height == target.Height;
    }

    private static GRBackendRenderTarget CreateGpuBackendRenderTarget(
        SkiaGpuRenderTargetHandle target) {
        return new GRBackendRenderTarget(
            target.Width,
            target.Height,
            sampleCount: 0,
            stencilBits: 0,
            glInfo: new GRGlFramebufferInfo(
                target.FramebufferId,
                WglNative.GlRgba8));
    }

    private static SKSurface CreateGpuSurface(
        ISkiaGpuContext gpuContext,
        GRBackendRenderTarget backendRenderTarget) {
        return SKSurface.Create(
            gpuContext.GpuContext,
            backendRenderTarget,
            GRSurfaceOrigin.TopLeft,
            SKColorType.Bgra8888) ??
            throw new InvalidOperationException(
                "Skia could not create a GPU-backed window surface.");
    }

    public RenderFrame Render(RenderScene scene) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Size != Descriptor.Size) {
            throw new ArgumentException("The render scene size does not match the target.", nameof(scene));
        }

        _gpuContext?.MakeCurrent();
        var damage = !_hasFrame
            ? DamageRegion.Full(Descriptor.Size)
            : scene.Damage;
        var canvas = _canvas;
        var saveCount = canvas.Save();
        try {
            canvas.ClipRect(
                new SKRect(
                    0,
                    0,
                    Descriptor.Size.Width,
                    Descriptor.Size.Height),
                SKClipOperation.Intersect,
                antialias: false);
            _sceneRenderer.Render(canvas, scene, damage);
        } finally {
            canvas.RestoreToCount(saveCount);
        }

        if (_gpuSurface is not null) {
            _sceneRenderer.Trim();
        } else {
            canvas.Flush();
            _sceneRenderer.Trim();
        }
        var statistics = scene.Statistics with {
            RasterCacheHits = _sceneRenderer.RasterCacheHits,
            RasterCacheMisses = _sceneRenderer.RasterCacheMisses,
            RasterCacheBytes = _sceneRenderer.RasterCacheBytes,
        };
        if (_gpuContext is not null) {
            var gpuTarget = _gpuTarget ?? throw new InvalidOperationException(
                "The GPU render target does not have a backing framebuffer.");
            _hasFrame = true;
            return new RenderFrame(
                Descriptor,
                RenderPixelFormat.Bgra8888Premultiplied,
                Descriptor.Size.Width * 4,
                new SkiaGpuFrameSurface(
                    this,
                    gpuTarget,
                    Descriptor.Size,
                    damage,
                    scene.VisualAccessibility),
                damage,
                statistics);
        }

        _pixelReader.Read(damage, scene.VisualAccessibility);
        _hasFrame = true;
        return new RenderFrame(
            Descriptor,
            RenderPixelFormat.Bgra8888Premultiplied,
            Descriptor.Size.Width * 4,
            _pixelReader.Pixels,
            damage,
            statistics);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = _pendingCleanupFailure;
        _pendingCleanupFailure = null;
        if (_gpuSurface is not null) {
            SkiaCleanup.Capture(ref failure, _gpuSurface.Dispose);
            if (_gpuBackendRenderTarget is not null) {
                SkiaCleanup.Capture(
                    ref failure,
                    _gpuBackendRenderTarget.Dispose);
            }
        }

        if (_gpuContext is not null && _gpuTarget is { } gpuTarget) {
            SkiaCleanup.Capture(
                ref failure,
                () => _gpuContext.DeleteTextureTarget(gpuTarget));
        } else {
            SkiaCleanup.Capture(ref failure, _canvas.Dispose);
            if (_bitmap is not null) {
                SkiaCleanup.Capture(ref failure, _bitmap.Dispose);
            }
        }

        SkiaCleanup.Capture(ref failure, _sceneRenderer.Dispose);
        _hasFrame = false;
        SkiaCleanup.ThrowIfFailed(failure);
    }

    internal ReadOnlyMemory<byte> ReadGpuPixels(
        DamageRegion damage,
        AccessibilityVisualOptions visualEffects) {
        _gpuContext!.MakeCurrent();
        _gpuContext.GpuContext.Flush();
        var surface = _gpuSurface ?? throw new InvalidOperationException(
            "The render target has no GPU surface to read.");
        _pixelReader.Read(surface, damage, visualEffects);
        return _pixelReader.Pixels;
    }
}
