using System.Runtime.InteropServices;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class EglGpuContext : ISkiaGpuContext {
    private readonly Dictionary<IntPtr, EglPresentationSurface> _presenters = [];
    private IntPtr _display;
    private IntPtr _waylandDisplay;
    private IntPtr _configuration;
    private IntPtr _context;
    private IntPtr _surface;
    private GRGlInterface? _glInterface;
    private bool _ownsWaylandDisplay;
    private bool _disposed;

    public EglGpuContext(IntPtr waylandDisplay = default) {
        if (!OperatingSystem.IsLinux()) {
            throw new PlatformNotSupportedException(
                "The EGL Skia GPU context can only run on Linux.");
        }

        _display = CreateDisplay(
            waylandDisplay,
            out _waylandDisplay,
            out var ownsWaylandDisplay);
        _ownsWaylandDisplay = ownsWaylandDisplay;
        try {
            if (EglNative.eglBindAPI(EglNative.EglOpenGlApi) == 0) {
                throw CreateException(
                    "EGL could not bind the desktop OpenGL API.");
            }

            _configuration = ChooseConfiguration(_display);
            _context = CreateContext(_display, _configuration);
            _surface = CreatePbufferSurface(_display, _configuration);
            MakeCurrent();
            _glInterface = GRGlInterface.CreateOpenGl(
                GetGlProcedureAddress) ??
                throw new InvalidOperationException(
                    "Skia could not create an EGL OpenGL interface.");
            GpuContext = GRContext.CreateGl(_glInterface) ??
                throw new InvalidOperationException(
                    "Skia could not create an EGL OpenGL GRContext.");
        } catch {
            _glInterface?.Dispose();
            _glInterface = null;
            CleanupNativeResources();
            throw;
        }
    }

    public GRContext GpuContext { get; private set; } = null!;

    public SkiaGpuRenderTargetHandle CreateTextureTarget(
        int width,
        int height) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MakeCurrent();
        var storageWidth = SkiaTextureCapacity.Get(width);
        var storageHeight = SkiaTextureCapacity.Get(height);
        EglNative.glGenTextures(1, out var texture);
        EglNative.glBindTexture(EglNative.GlTexture2D, texture);
        EglNative.glTexParameteri(
            EglNative.GlTexture2D,
            EglNative.GlTextureMinFilter,
            (int)EglNative.GlLinear);
        EglNative.glTexParameteri(
            EglNative.GlTexture2D,
            EglNative.GlTextureMagFilter,
            (int)EglNative.GlLinear);
        EglNative.glTexImage2D(
            EglNative.GlTexture2D,
            level: 0,
            internalFormat: (int)EglNative.GlRgba8,
            storageWidth,
            storageHeight,
            border: 0,
            EglNative.GlBgra,
            EglNative.GlUnsignedByte,
            IntPtr.Zero);
        EglNative.glGenFramebuffers(1, out var framebuffer);
        EglNative.glBindFramebuffer(
            EglNative.GlFramebuffer,
            framebuffer);
        EglNative.glFramebufferTexture2D(
            EglNative.GlFramebuffer,
            EglNative.GlColorAttachment0,
            EglNative.GlTexture2D,
            texture,
            level: 0);
        var status = EglNative.glCheckFramebufferStatus(
            EglNative.GlFramebuffer);
        EglNative.glBindFramebuffer(EglNative.GlFramebuffer, 0);
        if (status != EglNative.GlFramebufferComplete) {
            DeleteTextureTarget(new SkiaGpuRenderTargetHandle(
                texture,
                framebuffer,
                storageWidth,
                storageHeight));
            throw new InvalidOperationException(
                $"EGL OpenGL framebuffer is incomplete (0x{status:X8}).");
        }

        return new SkiaGpuRenderTargetHandle(
            texture,
            framebuffer,
            storageWidth,
            storageHeight);
    }

    public SkiaGpuRenderTargetHandle ResizeTextureTarget(
        SkiaGpuRenderTargetHandle target,
        int width,
        int height) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MakeCurrent();
        if (width <= target.Width && height <= target.Height) {
            return target;
        }

        var storageWidth = Math.Max(target.Width, SkiaTextureCapacity.Get(width));
        var storageHeight = Math.Max(target.Height, SkiaTextureCapacity.Get(height));
        EglNative.glBindTexture(EglNative.GlTexture2D, target.TextureId);
        EglNative.glTexImage2D(
            EglNative.GlTexture2D,
            level: 0,
            internalFormat: (int)EglNative.GlRgba8,
            storageWidth,
            storageHeight,
            border: 0,
            EglNative.GlBgra,
            EglNative.GlUnsignedByte,
            IntPtr.Zero);
        EglNative.glBindTexture(EglNative.GlTexture2D, 0);

        EglNative.glBindFramebuffer(
            EglNative.GlFramebuffer,
            target.FramebufferId);
        var status = EglNative.glCheckFramebufferStatus(
            EglNative.GlFramebuffer);
        EglNative.glBindFramebuffer(EglNative.GlFramebuffer, 0);
        if (status != EglNative.GlFramebufferComplete) {
            throw new InvalidOperationException(
                $"EGL OpenGL framebuffer is incomplete after resize (0x{status:X8}).");
        }

        return target with {
            Width = storageWidth,
            Height = storageHeight,
        };
    }

    public void DeleteTextureTarget(SkiaGpuRenderTargetHandle target) {
        if (_disposed) {
            return;
        }

        MakeCurrent();
        var framebuffer = target.FramebufferId;
        var texture = target.TextureId;
        EglNative.glDeleteFramebuffers(1, ref framebuffer);
        EglNative.glDeleteTextures(1, ref texture);
    }

    public void UploadTexture(
        SkiaGpuRenderTargetHandle target,
        ReadOnlyMemory<byte> pixels,
        int width,
        int height) {
        if (pixels.Length / 4 < (long)width * height) {
            throw new ArgumentException(
                "The upload buffer is smaller than the texture.",
                nameof(pixels));
        }

        MakeCurrent();
        EglNative.glBindTexture(EglNative.GlTexture2D, target.TextureId);
        if (MemoryMarshal.TryGetArray(pixels, out var segment) &&
            segment.Array is not null) {
            var handle = GCHandle.Alloc(
                segment.Array,
                GCHandleType.Pinned);
            try {
                EglNative.glTexSubImage2D(
                    EglNative.GlTexture2D,
                    level: 0,
                    xOffset: 0,
                    yOffset: 0,
                    width,
                    height,
                    EglNative.GlBgra,
                    EglNative.GlUnsignedByte,
                    IntPtr.Add(
                        handle.AddrOfPinnedObject(),
                        segment.Offset));
            } finally {
                handle.Free();
            }
        } else {
            var copy = pixels.ToArray();
            var handle = GCHandle.Alloc(copy, GCHandleType.Pinned);
            try {
                EglNative.glTexSubImage2D(
                    EglNative.GlTexture2D,
                    level: 0,
                    xOffset: 0,
                    yOffset: 0,
                    width,
                    height,
                    EglNative.GlBgra,
                    EglNative.GlUnsignedByte,
                    handle.AddrOfPinnedObject());
            } finally {
                handle.Free();
            }
        }

        EglNative.glBindTexture(EglNative.GlTexture2D, 0);
    }

    public bool TryPresent(
        IntPtr nativeSurface,
        SkiaGpuRenderTargetHandle target,
        int width,
        int height) {
        if (_disposed || _waylandDisplay == IntPtr.Zero ||
            nativeSurface == IntPtr.Zero || width <= 0 || height <= 0) {
            return false;
        }

        if (!TryGetPresenter(
                nativeSurface,
                width,
                height,
                out var presenter)) {
            return false;
        }

        bool presented;
        try {
            presented = presenter.Present(this, target, width, height);
        } finally {
            // Presentation uses raw GL for the final blit and changes state
            // that Skia tracks internally. Without invalidating that cache,
            // later draws can inherit the wrong framebuffer/viewport/blend
            // state and produce missing glyphs or partially black frames.
            GpuContext.ResetContext(GRGlBackendState.All);
        }

        if (presented) {
            return true;
        }

        _presenters.Remove(nativeSurface);
        presenter.Dispose();
        return false;
    }

    public void ReleaseNativeSurface(IntPtr nativeSurface) {
        if (nativeSurface == IntPtr.Zero ||
            !_presenters.Remove(nativeSurface, out var presenter)) {
            return;
        }

        presenter.Dispose();
    }

    public void MakeCurrent() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMakeCurrent(_surface)) {
            throw CreateException(
                "EGL could not make the OpenGL context current.");
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        if (GpuContext is not null) {
            SkiaCleanup.Capture(ref failure, GpuContext.Dispose);
        }

        if (_glInterface is not null) {
            SkiaCleanup.Capture(ref failure, _glInterface.Dispose);
            _glInterface = null;
        }

        CleanupNativeResources(ref failure);
        SkiaCleanup.ThrowIfFailed(failure);
    }

    private bool TryGetPresenter(
        IntPtr nativeSurface,
        int width,
        int height,
        out EglPresentationSurface presenter) {
        if (_presenters.TryGetValue(nativeSurface, out presenter!)) {
            return true;
        }

        try {
            presenter = EglPresentationSurface.Create(
                this,
                nativeSurface,
                width,
                height)!;
        } catch (DllNotFoundException) {
            presenter = null!;
            return false;
        } catch (EntryPointNotFoundException) {
            presenter = null!;
            return false;
        } catch (BadImageFormatException) {
            presenter = null!;
            return false;
        }

        if (presenter is null) {
            return false;
        }

        _presenters.Add(nativeSurface, presenter);
        return true;
    }

}
