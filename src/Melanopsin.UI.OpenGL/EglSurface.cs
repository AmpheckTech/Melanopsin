using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.OpenGL;

internal sealed class EglSurface : IOpenGLSurface {
    private static readonly int[] ConfigurationAttributes = [
        EglNative.EglSurfaceType,
        EglNative.EglPbufferBit,
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

    private IntPtr _display;
    private IntPtr _waylandDisplay;
    private IntPtr _configuration;
    private IntPtr _context;
    private IntPtr _surface;
    private byte[] _readback = [];
    private byte[] _pixels = [];
    private int _width;
    private int _height;
    private bool _disposed;

    public EglSurface() {
        _display = CreateDisplay(out _waylandDisplay);
        try {
            if (EglNative.eglBindAPI(EglNative.EglOpenGlApi) == 0) {
                throw CreateException("EGL could not bind the desktop OpenGL API.");
            }

            _configuration = ChooseConfiguration(_display);
            _context = CreateContext(_display, _configuration);
            _surface = CreatePbufferSurface(_display, _configuration, 1, 1);
            MakeCurrent();
        } catch {
            CleanupNativeResources();
            throw;
        }
    }

    public ReadOnlyMemory<byte> Render(
        int width,
        int height,
        Color clearColor,
        Action<int, int>? content) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        EnsureSize(width, height);
        MakeCurrent();
        return OpenGLFrameRenderer.Render(
            width,
            height,
            clearColor,
            content,
            readBuffer: OpenGLNative.GlFront,
            ref _readback,
            ref _pixels);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        CaptureCleanupFailure(
            ref failure,
            () => _ = EglNative.eglMakeCurrent(
                _display,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero));
        CaptureCleanupFailure(
            ref failure,
            () => _ = EglNative.eglDestroyContext(_display, _context));
        CaptureCleanupFailure(
            ref failure,
            () => _ = EglNative.eglDestroySurface(_display, _surface));
        CaptureCleanupFailure(
            ref failure,
            () => _ = EglNative.eglTerminate(_display));
        if (_waylandDisplay != IntPtr.Zero) {
            CaptureCleanupFailure(
                ref failure,
                () => EglNative.wl_display_disconnect(_waylandDisplay));
        }

        _display = IntPtr.Zero;
        _waylandDisplay = IntPtr.Zero;
        _configuration = IntPtr.Zero;
        _context = IntPtr.Zero;
        _surface = IntPtr.Zero;
        ThrowIfFailed(failure);
    }

    private static IntPtr CreateDisplay(out IntPtr waylandDisplay) {
        var getPlatformDisplay = ResolveGetPlatformDisplay();
        if (getPlatformDisplay is not null &&
            !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) {
            waylandDisplay = ConnectWaylandDisplay();
            if (waylandDisplay != IntPtr.Zero) {
                var display = getPlatformDisplay(
                    EglNative.EglPlatformWayland,
                    waylandDisplay,
                    attributes: IntPtr.Zero);
                if (TryInitialize(display)) {
                    return display;
                }

                EglNative.wl_display_disconnect(waylandDisplay);
                waylandDisplay = IntPtr.Zero;
            }
        }

        waylandDisplay = IntPtr.Zero;
        if (getPlatformDisplay is not null) {
            var display = getPlatformDisplay(
                EglNative.EglPlatformSurfaceless,
                nativeDisplay: IntPtr.Zero,
                attributes: IntPtr.Zero);
            if (TryInitialize(display)) {
                return display;
            }
        }

        throw new InvalidOperationException(
            "Linux OpenGL requires EGL Wayland or surfaceless platform support.");
    }

    private static EglNative.GetPlatformDisplay? ResolveGetPlatformDisplay() {
        var address = EglNative.eglGetProcAddress("eglGetPlatformDisplayEXT");
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

    private static unsafe IntPtr ChooseConfiguration(IntPtr display) {
        fixed (int* attributes = ConfigurationAttributes) {
            if (EglNative.eglChooseConfig(
                    display,
                    attributes,
                    out var configuration,
                    configurationSize: 1,
                    out var configurationCount) == 0 ||
                configurationCount == 0) {
                throw CreateException(
                    "EGL could not find an OpenGL pbuffer configuration.");
            }

            return configuration;
        }
    }

    private static unsafe IntPtr CreateContext(
        IntPtr display,
        IntPtr configuration) {
        int* attributes = stackalloc int[] { EglNative.EglNone };
        var context = EglNative.eglCreateContext(
            display,
            configuration,
            shareContext: IntPtr.Zero,
            attributes);
        if (context == IntPtr.Zero) {
            throw CreateException("EGL could not create an OpenGL context.");
        }

        return context;
    }

    private static unsafe IntPtr CreatePbufferSurface(
        IntPtr display,
        IntPtr configuration,
        int width,
        int height) {
        int* attributes = stackalloc int[] {
            EglNative.EglWidth,
            width,
            EglNative.EglHeight,
            height,
            EglNative.EglNone,
        };
        var surface = EglNative.eglCreatePbufferSurface(
            display,
            configuration,
            attributes);
        if (surface == IntPtr.Zero) {
            throw CreateException("EGL could not create a pbuffer surface.");
        }

        return surface;
    }

    private void EnsureSize(int width, int height) {
        if (_width == width && _height == height) {
            return;
        }

        var nextSurface = CreatePbufferSurface(
            _display,
            _configuration,
            width,
            height);
        if (EglNative.eglMakeCurrent(
                _display,
                nextSurface,
                nextSurface,
                _context) == 0) {
            _ = EglNative.eglDestroySurface(_display, nextSurface);
            throw CreateException(
                "EGL could not make the resized pbuffer current.");
        }

        var previousSurface = _surface;
        _surface = nextSurface;
        _width = width;
        _height = height;
        if (previousSurface != IntPtr.Zero) {
            _ = EglNative.eglDestroySurface(_display, previousSurface);
        }
    }

    private void MakeCurrent() {
        if (EglNative.eglMakeCurrent(
                _display,
                _surface,
                _surface,
                _context) == 0) {
            throw CreateException(
                "EGL could not make the OpenGL context current.");
        }
    }

    private void CleanupNativeResources() {
        if (_display != IntPtr.Zero) {
            _ = EglNative.eglMakeCurrent(
                _display,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        if (_context != IntPtr.Zero) {
            _ = EglNative.eglDestroyContext(_display, _context);
        }

        if (_surface != IntPtr.Zero) {
            _ = EglNative.eglDestroySurface(_display, _surface);
        }

        if (_display != IntPtr.Zero) {
            _ = EglNative.eglTerminate(_display);
        }

        if (_waylandDisplay != IntPtr.Zero) {
            EglNative.wl_display_disconnect(_waylandDisplay);
        }

        _display = IntPtr.Zero;
        _waylandDisplay = IntPtr.Zero;
        _configuration = IntPtr.Zero;
        _context = IntPtr.Zero;
        _surface = IntPtr.Zero;
    }

    private static InvalidOperationException CreateException(string message) {
        return new InvalidOperationException(
            $"{message} EGL error 0x{EglNative.eglGetError():X4}.");
    }

    private static void CaptureCleanupFailure(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception exception) {
            failure = failure is null
                ? exception
                : failure is AggregateException aggregate
                    ? new AggregateException(
                        aggregate.InnerExceptions.Append(exception))
                    : new AggregateException(failure, exception);
        }
    }

    private static void ThrowIfFailed(Exception? failure) {
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
