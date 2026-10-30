using System.Runtime.InteropServices;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class EglGpuContext : ISkiaGpuContext {
    private static readonly int[] ConfigurationAttributes = [
        EglNative.EglSurfaceType,
        EglNative.EglPbufferBit | EglNative.EglWindowBit,
        EglNative.EglRenderableType,
        EglNative.EglOpenGlBit,
        EglNative.EglRedSize,
        8,
        EglNative.EglGreenSize,
        8,
        EglNative.EglBlueSize,
        8,
        EglNative.EglAlphaSize,
        8,
        EglNative.EglDepthSize,
        24,
        EglNative.EglNone,
    ];

    private static readonly Lazy<IntPtr> GlLibrary = new(
        static () => NativeLibrary.TryLoad(
            "libGL.so.1",
            out var handle)
            ? handle
            : IntPtr.Zero);

    private static readonly Lazy<IntPtr> EglLibrary = new(
        static () => NativeLibrary.TryLoad(
            "libEGL.so.1",
            out var handle)
            ? handle
            : IntPtr.Zero);

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
        var storageWidth = TextureCapacity(width);
        var storageHeight = TextureCapacity(height);
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

        var storageWidth = Math.Max(target.Width, TextureCapacity(width));
        var storageHeight = Math.Max(target.Height, TextureCapacity(height));
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
        if (pixels.Length < width * height * 4) {
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

    private static IntPtr CreateDisplay(
        IntPtr nativeWaylandDisplay,
        out IntPtr waylandDisplay,
        out bool ownsWaylandDisplay) {
        var getPlatformDisplay = ResolveGetPlatformDisplay();
        if (nativeWaylandDisplay != IntPtr.Zero) {
            if (getPlatformDisplay is not null) {
                var display = getPlatformDisplay(
                    EglNative.EglPlatformWayland,
                    nativeWaylandDisplay,
                    attributes: IntPtr.Zero);
                if (TryInitialize(display)) {
                    waylandDisplay = nativeWaylandDisplay;
                    ownsWaylandDisplay = false;
                    return display;
                }
            }

            // EGL 1.4 drivers support this legacy path on Wayland even when
            // neither eglGetPlatformDisplay nor its EXT variant is available.
            // The display must remain the host's wl_display: a surfaceless or
            // independently connected display cannot present the wl_surface.
            var legacyDisplay = EglNative.eglGetDisplay(nativeWaylandDisplay);
            if (TryInitialize(legacyDisplay)) {
                waylandDisplay = nativeWaylandDisplay;
                ownsWaylandDisplay = false;
                return legacyDisplay;
            }

            waylandDisplay = IntPtr.Zero;
            ownsWaylandDisplay = false;
            throw new InvalidOperationException(
                "EGL could not initialize the supplied Wayland display. " +
                "The driver must support EGL_PLATFORM_WAYLAND_KHR or the " +
                "legacy wl_display EGL path.");
        }

        // Standalone/headless usage can safely open its own Wayland
        // connection when one is available. Window hosts always pass their
        // display explicitly.
        if (getPlatformDisplay is not null &&
            !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) {
            var connectedDisplay = ConnectWaylandDisplay();
            if (connectedDisplay != IntPtr.Zero) {
                var display = getPlatformDisplay(
                    EglNative.EglPlatformWayland,
                    connectedDisplay,
                    attributes: IntPtr.Zero);
                if (TryInitialize(display)) {
                    waylandDisplay = connectedDisplay;
                    ownsWaylandDisplay = true;
                    return display;
                }

                EglNative.wl_display_disconnect(connectedDisplay);
            }
        }

        waylandDisplay = IntPtr.Zero;
        ownsWaylandDisplay = false;
        if (getPlatformDisplay is not null) {
            var display = getPlatformDisplay(
                EglNative.EglPlatformSurfaceless,
                nativeDisplay: IntPtr.Zero,
                attributes: IntPtr.Zero);
            if (TryInitialize(display)) {
                return display;
            }
        }

        var defaultDisplay = EglNative.eglGetDisplay(IntPtr.Zero);
        if (TryInitialize(defaultDisplay)) {
            return defaultDisplay;
        }

        throw new InvalidOperationException(
            "EGL could not initialize a Wayland, surfaceless, or default display.");
    }

    private static IntPtr GetGlProcedureAddress(string name) {
        var address = EglNative.eglGetProcAddress(name);
        if (address != IntPtr.Zero) {
            return address;
        }

        var library = GlLibrary.Value;
        return library != IntPtr.Zero &&
            NativeLibrary.TryGetExport(
                library,
                name,
                out address)
            ? address
            : IntPtr.Zero;
    }

    private static EglNative.GetPlatformDisplay? ResolveGetPlatformDisplay() {
        // Prefer the EGL 1.5 core entry point. eglGetProcAddress is allowed to
        // return core functions too, but drivers differ in whether they do.
        var address = IntPtr.Zero;
        var library = EglLibrary.Value;
        if (library != IntPtr.Zero) {
            _ = NativeLibrary.TryGetExport(
                library,
                "eglGetPlatformDisplay",
                out address);
        }

        if (address == IntPtr.Zero) {
            address = EglNative.eglGetProcAddress("eglGetPlatformDisplay");
        }

        if (address == IntPtr.Zero) {
            address = EglNative.eglGetProcAddress("eglGetPlatformDisplayEXT");
        }

        return address == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<EglNative.GetPlatformDisplay>(
                address);
    }

    private static IntPtr ConnectWaylandDisplay() {
        try {
            return EglNative.wl_display_connect(IntPtr.Zero);
        } catch (DllNotFoundException) {
            return IntPtr.Zero;
        } catch (EntryPointNotFoundException) {
            return IntPtr.Zero;
        }
    }

    private static bool TryInitialize(IntPtr display) {
        return display != IntPtr.Zero &&
            EglNative.eglInitialize(display, out _, out _) != 0;
    }

    private static IntPtr ChooseConfiguration(IntPtr display) {
        if (EglNative.eglChooseConfig(
                display,
                ConfigurationAttributes,
                out var configuration,
                configurationSize: 1,
                out var configurationCount) == 0 ||
            configurationCount == 0) {
            throw CreateException(
                "EGL could not find an OpenGL pbuffer configuration.");
        }

        return configuration;
    }

    private static IntPtr CreateContext(
        IntPtr display,
        IntPtr configuration) {
        var attributes = new[] { EglNative.EglNone };
        var context = EglNative.eglCreateContext(
            display,
            configuration,
            shareContext: IntPtr.Zero,
            attributes);
        if (context == IntPtr.Zero) {
            throw CreateException(
                "EGL could not create an OpenGL context.");
        }

        return context;
    }

    private static IntPtr CreatePbufferSurface(
        IntPtr display,
        IntPtr configuration) {
        var attributes = new[] {
            EglNative.EglWidth,
            1,
            EglNative.EglHeight,
            1,
            EglNative.EglNone,
        };
        var surface = EglNative.eglCreatePbufferSurface(
            display,
            configuration,
            attributes);
        if (surface == IntPtr.Zero) {
            throw CreateException(
                "EGL could not create an OpenGL pbuffer surface.");
        }

        return surface;
    }

    private bool TryMakeCurrent(IntPtr surface) {
        return _display != IntPtr.Zero &&
            surface != IntPtr.Zero &&
            _context != IntPtr.Zero &&
            EglNative.eglMakeCurrent(
                _display,
                surface,
                surface,
                _context) != 0;
    }

    private static InvalidOperationException CreateException(string message) {
        return new InvalidOperationException(
            $"{message} EGL error 0x{EglNative.eglGetError():X4}.");
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

    private static int TextureCapacity(int dimension) {
        if (dimension <= 0) {
            throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        var capacity = (long)dimension + Math.Max(64, dimension / 8L);
        capacity = (capacity + 255) & ~255L;
        return capacity > int.MaxValue
            ? int.MaxValue
            : (int)capacity;
    }

    private sealed class EglPresentationSurface : IDisposable {
        private readonly EglGpuContext _owner;
        private readonly IntPtr _nativeSurface;
        private IntPtr _eglWindow;
        private IntPtr _eglSurface;
        private int _width;
        private int _height;
        private bool _disposed;

        private EglPresentationSurface(
            EglGpuContext owner,
            IntPtr nativeSurface,
            IntPtr eglWindow,
            IntPtr eglSurface,
            int width,
            int height) {
            _owner = owner;
            _nativeSurface = nativeSurface;
            _eglWindow = eglWindow;
            _eglSurface = eglSurface;
            _width = width;
            _height = height;
        }

        public static EglPresentationSurface? Create(
            EglGpuContext owner,
            IntPtr nativeSurface,
            int width,
            int height) {
            var eglWindow = EglNative.wl_egl_window_create(
                nativeSurface,
                width,
                height);
            if (eglWindow == IntPtr.Zero) {
                return null;
            }

            var eglSurface = EglNative.eglCreateWindowSurface(
                owner._display,
                owner._configuration,
                eglWindow,
                [EglNative.EglNone]);
            if (eglSurface == IntPtr.Zero) {
                EglNative.wl_egl_window_destroy(eglWindow);
                return null;
            }

            return new EglPresentationSurface(
                owner,
                nativeSurface,
                eglWindow,
                eglSurface,
                width,
                height);
        }

        public bool Present(
            EglGpuContext owner,
            SkiaGpuRenderTargetHandle target,
            int width,
            int height) {
            if (_disposed || owner != _owner || target.TextureId == 0 ||
                width <= 0 || height <= 0 ||
                width > target.Width || height > target.Height) {
                return false;
            }

            EnsureSize(width, height);

            if (!owner.TryMakeCurrent(_eglSurface)) {
                return false;
            }

            try {
                // Some Mesa versions reset the swap interval after a resize or
                // surface reconfiguration. Re-assert zero to avoid adding a
                // second compositor-level presentation delay.
                _ = EglNative.eglSwapInterval(owner._display, 0);
                if (!DrawTexture(target, width, height)) {
                    return false;
                }

                return EglNative.eglSwapBuffers(
                    owner._display,
                    _eglSurface) != 0;
            } finally {
                _ = owner.TryMakeCurrent(owner._surface);
            }
        }

        private void EnsureSize(int width, int height) {
            if (width == _width && height == _height) {
                return;
            }

            EglNative.wl_egl_window_resize(
                _eglWindow,
                width,
                height,
                dx: 0,
                dy: 0);
            _width = width;
            _height = height;
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            if (_eglSurface != IntPtr.Zero && _owner._display != IntPtr.Zero) {
                _ = EglNative.eglDestroySurface(
                    _owner._display,
                    _eglSurface);
            }

            if (_eglWindow != IntPtr.Zero) {
                EglNative.wl_egl_window_destroy(_eglWindow);
            }

            _eglSurface = IntPtr.Zero;
            _eglWindow = IntPtr.Zero;
        }

        private static bool DrawTexture(
            SkiaGpuRenderTargetHandle target,
            int width,
            int height) {
            while (EglNative.glGetError() != EglNative.GlNoError) {
            }

            EglNative.glDisable(EglNative.GlDepthTest);
            EglNative.glDisable(EglNative.GlBlend);
            EglNative.glDisable(EglNative.GlScissorTest);
            EglNative.glDisable(EglNative.GlCullFace);
            // Skia can leave an offscreen framebuffer bound as the read
            // framebuffer. Presentation only needs the default draw
            // framebuffer; changing GL_READ_BUFFER here is invalid while the
            // offscreen read framebuffer is still bound on Mesa.
            EglNative.glBindFramebuffer(
                EglNative.GlDrawFramebuffer,
                0);
            SelectBackBuffer();
            EglNative.glViewport(0, 0, width, height);
            EglNative.glClearColor(0, 0, 0, 1);
            EglNative.glClear(EglNative.GlColorBufferBit);
            // Wayland top-level windows in Melanopsin are opaque. Preserve the
            // alpha written by the clear instead of copying the offscreen
            // surface's alpha into the window back buffer. Otherwise resize
            // frames can briefly become translucent or black where Skia has
            // not painted an opaque pixel.
            EglNative.glColorMask(1, 1, 1, 0);
            try {
                EglNative.glBindFramebuffer(
                    EglNative.GlReadFramebuffer,
                    target.FramebufferId);
                EglNative.glReadBuffer(EglNative.GlColorAttachment0);
                EglNative.glBlitFramebuffer(
                    sourceX0: 0,
                    // Skia's FBO stores the first canvas row at the bottom of
                    // the GL texture. Flip while copying into the Wayland back
                    // buffer; otherwise the presented window is upside down.
                    sourceY0: height,
                    sourceX1: width,
                    sourceY1: 0,
                    destinationX0: 0,
                    destinationY0: 0,
                    destinationX1: width,
                    destinationY1: height,
                    EglNative.GlColorBufferBit,
                    EglNative.GlNearest);
            } finally {
                EglNative.glColorMask(1, 1, 1, 1);
                EglNative.glBindFramebuffer(EglNative.GlFramebuffer, 0);
            }

            return EglNative.glGetError() == EglNative.GlNoError;
        }

        private static void SelectBackBuffer() {
            // NVIDIA's EGL Wayland implementation can incorrectly keep the
            // default draw buffer on GL_FRONT after a surface resize.
            // Explicitly selecting GL_BACK avoids presenting a stale or empty
            // front buffer while the compositor is reconfiguring the window.
            EglNative.glDrawBuffer(EglNative.GlBack);
        }
    }

    private void CleanupNativeResources() {
        Exception? failure = null;
        CleanupNativeResources(ref failure);
    }

    private void CleanupNativeResources(ref Exception? failure) {
        foreach (var presenter in _presenters.Values) {
            Capture(ref failure, presenter.Dispose);
        }
        _presenters.Clear();

        if (_display != IntPtr.Zero) {
            Capture(
                ref failure,
                () => _ = EglNative.eglMakeCurrent(
                    _display,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero));
        }

        if (_context != IntPtr.Zero) {
            Capture(
                ref failure,
                () => _ = EglNative.eglDestroyContext(
                    _display,
                    _context));
            _context = IntPtr.Zero;
        }

        if (_surface != IntPtr.Zero) {
            Capture(
                ref failure,
                () => _ = EglNative.eglDestroySurface(
                    _display,
                    _surface));
            _surface = IntPtr.Zero;
        }

        if (_display != IntPtr.Zero) {
            Capture(
                ref failure,
                () => _ = EglNative.eglTerminate(_display));
            _display = IntPtr.Zero;
        }

        if (_waylandDisplay != IntPtr.Zero && _ownsWaylandDisplay) {
            Capture(
                ref failure,
                () => EglNative.wl_display_disconnect(_waylandDisplay));
        }
        _waylandDisplay = IntPtr.Zero;

        _configuration = IntPtr.Zero;
    }

    private static void Capture(
        ref Exception? failure,
        Action action) {
        SkiaCleanup.Capture(ref failure, action);
    }
}
