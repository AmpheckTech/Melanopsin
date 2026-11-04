using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaRenderTarget :
    IResizableRenderTarget,
    IRenderFrameLeaseOwner {
    private const long LeaseHeldBit = 1;
    private const long MaxLeaseGeneration = long.MaxValue >> 1;

    private SKBitmap? _bitmap;
    private SKSurface? _gpuSurface;
    private readonly ISkiaGpuContext? _gpuContext;
    private GRBackendRenderTarget? _gpuBackendRenderTarget;
    private SkiaGpuRenderTargetHandle? _gpuTarget;
    private Exception? _pendingCleanupFailure;
    private readonly SkiaSceneRenderer _sceneRenderer;
    private readonly SkiaPixelReader _pixelReader;
    private readonly SkiaFrameSurface _frameSurface;
    private SKCanvas _canvas;
    private bool _hasFrame;
    private long _leaseState;
    private DamageRegion _damage;
    private RenderStatistics _statistics;
    private AccessibilityVisualOptions _visualAccessibility;
    private bool _disposed;

    public SkiaRenderTarget(
        RenderTargetDescriptor descriptor,
        FontFace? primaryFont,
        SKTypeface? typeface,
        SkiaRasterCacheOptions rasterCacheOptions) {
        Descriptor = descriptor;
        var bitmap = new SKBitmap(
            descriptor.Size.Width,
            descriptor.Size.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        _bitmap = bitmap;
        _canvas = new SKCanvas(bitmap);
        _pixelReader = new SkiaPixelReader(descriptor.Size, bitmap);
        _frameSurface = new SkiaFrameSurface(this);
        _damage = DamageRegion.None(descriptor.Size);
        _sceneRenderer = new SkiaSceneRenderer(
            primaryFont,
            typeface,
            gpuContext: null,
            rasterCacheOptions);
    }

    internal SkiaRenderTarget(
        RenderTargetDescriptor descriptor,
        FontFace? primaryFont,
        SKTypeface? typeface,
        ISkiaGpuContext gpuContext,
        SkiaRasterCacheOptions rasterCacheOptions) {
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
            _frameSurface = new SkiaFrameSurface(this);
            _damage = DamageRegion.None(descriptor.Size);
            _sceneRenderer = new SkiaSceneRenderer(
                primaryFont,
                typeface,
                gpuContext,
                rasterCacheOptions);
        } catch {
            surface?.Dispose();
            backendRenderTarget?.Dispose();
            gpuContext.DeleteTextureTarget(target);
            throw;
        }
    }

    public RenderTargetDescriptor Descriptor { get; private set; }

    public bool IsFrameAvailable =>
        !_disposed && (Volatile.Read(ref _leaseState) & LeaseHeldBit) == 0;

    internal ISkiaGpuContext? GpuContext => _gpuContext;

    internal SkiaGpuRenderTargetHandle? GpuTarget => _gpuTarget;

    public bool TryResize(RenderTargetDescriptor descriptor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsFrameAvailable) {
            return false;
        }

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
        _damage = DamageRegion.None(size);
        _pixelReader.ResizeCpu(size, bitmap);
        InvalidateFrame();
        return true;
    }

    public bool TryRender(RenderScene scene, out RenderFrameLease frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Size != Descriptor.Size) {
            throw new ArgumentException("The render scene size does not match the target.", nameof(scene));
        }

        if (!TryAcquireLease(out var generation)) {
            frame = default;
            return false;
        }

        _gpuContext?.MakeCurrent();
        var damage = !_hasFrame
            ? DamageRegion.Full(Descriptor.Size)
            : scene.Damage;
        try {
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

            _statistics = scene.Statistics with {
                RasterCacheHits = _sceneRenderer.RasterCacheHits,
                RasterCacheMisses = _sceneRenderer.RasterCacheMisses,
                RasterCacheBytes = _sceneRenderer.RasterCacheBytes,
            };
            _damage = damage;
            _visualAccessibility = scene.VisualAccessibility;
            _hasFrame = true;
            frame = new RenderFrameLease(this, generation);
            return true;
        } catch {
            ((IRenderFrameLeaseOwner)this).ReleaseLease(generation);
            frame = default;
            throw;
        }
    }

    bool IRenderFrameLeaseOwner.IsLeaseValid(long generation) {
        return !_disposed &&
            Volatile.Read(ref _leaseState) == EncodeLeaseState(generation, held: true);
    }

    long IRenderFrameLeaseOwner.GetSequence(long generation) {
        ValidateLease(generation);
        return generation;
    }

    IRenderSurface IRenderFrameLeaseOwner.GetSurface(long generation) {
        ValidateLease(generation);
        return _frameSurface;
    }

    DamageRegion IRenderFrameLeaseOwner.GetDamage(long generation) {
        ValidateLease(generation);
        return _damage;
    }

    RenderStatistics IRenderFrameLeaseOwner.GetStatistics(long generation) {
        ValidateLease(generation);
        return _statistics;
    }

    AccessibilityVisualOptions IRenderFrameLeaseOwner.GetVisualAccessibility(
        long generation) {
        ValidateLease(generation);
        return _visualAccessibility;
    }

    void IRenderFrameLeaseOwner.ReleaseLease(long generation) {
        _ = Interlocked.CompareExchange(
            ref _leaseState,
            EncodeLeaseState(generation, held: false),
            EncodeLeaseState(generation, held: true));
    }

    internal bool TryCopyPixels(
        DamageRegion damage,
        AccessibilityVisualOptions visualAccessibility,
        Span<byte> destination,
        int stride) {
        if (_disposed || damage.Size != Descriptor.Size) {
            return false;
        }

        try {
            if (_gpuContext is { } gpuContext) {
                gpuContext.MakeCurrent();
                gpuContext.GpuContext.Flush();
            }

            _pixelReader.Read(damage, visualAccessibility);
            _pixelReader.CopyTo(destination, stride);
            return true;
        } catch {
            return false;
        }
    }

    internal bool TryPresentNative(IntPtr target) {
        if (_disposed ||
            _gpuContext is not { } gpuContext ||
            _gpuTarget is not { } gpuTarget ||
            _visualAccessibility != default) {
            return false;
        }

        try {
            gpuContext.MakeCurrent();
            gpuContext.GpuContext.Flush();
            return gpuContext.TryPresent(
                target,
                gpuTarget,
                Descriptor.Size.Width,
                Descriptor.Size.Height);
        } catch {
            return false;
        }
    }

    internal void ReleaseNativeTarget(IntPtr target) {
        _gpuContext?.ReleaseNativeSurface(target);
    }

    private bool TryAcquireLease(out long generation) {
        while (true) {
            var state = Volatile.Read(ref _leaseState);
            if ((state & LeaseHeldBit) != 0) {
                generation = 0;
                return false;
            }

            generation = (state >> 1) + 1;
            if (generation > MaxLeaseGeneration) {
                throw new InvalidOperationException(
                    "The render target exhausted its frame lease generations.");
            }

            if (Interlocked.CompareExchange(
                    ref _leaseState,
                    EncodeLeaseState(generation, held: true),
                    state) == state) {
                return true;
            }
        }
    }

    private static long EncodeLeaseState(long generation, bool held) {
        if (generation is < 0 or > MaxLeaseGeneration) {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        return (generation << 1) | (held ? LeaseHeldBit : 0);
    }

    private void ValidateLease(long generation) {
        if (!((IRenderFrameLeaseOwner)this).IsLeaseValid(generation)) {
            throw new InvalidOperationException(
                "The render frame lease is stale or has already been released.");
        }
    }

    private void InvalidateFrame() {
        _hasFrame = false;
        _damage = DamageRegion.None(Descriptor.Size);
        _statistics = default;
        _visualAccessibility = default;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _leaseState, 0);
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
}
