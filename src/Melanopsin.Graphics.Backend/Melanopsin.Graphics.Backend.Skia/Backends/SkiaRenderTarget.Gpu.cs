using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaRenderTarget {
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

            // Most resize steps remain inside the texture's slack capacity.
            // Keep the surface in that case and clip rendering to the logical
            // viewport so the spare capacity never reaches the frame.
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
            // Window.Resize replaces a target when resizing cannot be completed.
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
}
