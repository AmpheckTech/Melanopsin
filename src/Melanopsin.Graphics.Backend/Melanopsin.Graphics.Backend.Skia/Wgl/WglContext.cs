// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class WglContext : ISkiaGpuContext {
    private static readonly object RegistrationLock = new();
    private static readonly WglNative.WindowProcedure WindowProcedure =
        static (window, message, wParam, lParam) =>
            WglNative.DefWindowProcW(window, message, wParam, lParam);
    private static IntPtr _instance;
    private static string? _className;
    private readonly IntPtr _window;
    private readonly IntPtr _deviceContext;
    private readonly IntPtr _context;
    private readonly Dictionary<IntPtr, WglPresentationSurface> _presenters = [];
    private GRGlInterface? _glInterface;
    private readonly WglNative.GenFramebuffers _glGenFramebuffers;
    private readonly WglNative.DeleteFramebuffers _glDeleteFramebuffers;
    private readonly WglNative.BindFramebuffer _glBindFramebuffer;
    private readonly WglNative.FramebufferTexture2D _glFramebufferTexture2D;
    private readonly WglNative.CheckFramebufferStatus _glCheckFramebufferStatus;
    private bool _disposed;

    public WglContext() {
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(
                "The WGL GPU context can only run on Windows.");
        }

        EnsureWindowClass();
        _window = WglNative.CreateWindowExW(
            extendedStyle: 0,
            _className!,
            "Melanopsin Skia GPU surface",
            WglNative.WsPopup,
            x: 0,
            y: 0,
            width: 1,
            height: 1,
            parent: IntPtr.Zero,
            menu: IntPtr.Zero,
            _instance,
            parameter: IntPtr.Zero);
        if (_window == IntPtr.Zero) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not create the WGL window.");
        }

        _deviceContext = WglNative.GetDC(_window);
        if (_deviceContext == IntPtr.Zero) {
            WglNative.DestroyWindow(_window);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not acquire the WGL device context.");
        }

        var format = new PixelFormatDescriptor {
            Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
            Version = 1,
            Flags = WglNative.PfdDrawToWindow |
                WglNative.PfdSupportOpenGL |
                WglNative.PfdDoubleBuffer,
            PixelType = WglNative.PfdTypeRgba,
            ColorBits = 32,
            RedBits = 8,
            GreenBits = 8,
            BlueBits = 8,
            AlphaBits = 8,
            DepthBits = 24,
        };
        var pixelFormat = WglNative.ChoosePixelFormat(
            _deviceContext,
            ref format);
        if (pixelFormat == 0 ||
            !WglNative.SetPixelFormat(
                _deviceContext,
                pixelFormat,
                ref format)) {
            DisposeHandles();
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not configure a WGL pixel format.");
        }

        _context = WglNative.wglCreateContext(_deviceContext);
        if (_context == IntPtr.Zero) {
            DisposeHandles();
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "wglCreateContext failed.");
        }

        MakeCurrent();
        _glGenFramebuffers = LoadProcedure<WglNative.GenFramebuffers>(
            "glGenFramebuffers",
            "glGenFramebuffersEXT");
        _glDeleteFramebuffers = LoadProcedure<WglNative.DeleteFramebuffers>(
            "glDeleteFramebuffers",
            "glDeleteFramebuffersEXT");
        _glBindFramebuffer = LoadProcedure<WglNative.BindFramebuffer>(
            "glBindFramebuffer",
            "glBindFramebufferEXT");
        _glFramebufferTexture2D =
            LoadProcedure<WglNative.FramebufferTexture2D>(
                "glFramebufferTexture2D",
                "glFramebufferTexture2DEXT");
        _glCheckFramebufferStatus =
            LoadProcedure<WglNative.CheckFramebufferStatus>(
                "glCheckFramebufferStatus",
                "glCheckFramebufferStatusEXT");
        _glInterface = GRGlInterface.Create() ??
            throw new InvalidOperationException(
                "Skia could not create a desktop OpenGL interface.");
        GpuContext = GRContext.CreateGl(_glInterface) ??
            throw new InvalidOperationException(
                "Skia could not create an OpenGL GRContext.");
    }

    public GRContext GpuContext { get; private set; } = null!;

    public bool TryPresent(
        IntPtr window,
        SkiaGpuRenderTargetHandle target,
        int width,
        int height) {
        if (_disposed || window == IntPtr.Zero) {
            return false;
        }

        if (!_presenters.TryGetValue(window, out var presenter)) {
            try {
                presenter = new WglPresentationSurface(this, window);
                _presenters[window] = presenter;
            } catch (Win32Exception) {
                return false;
            }
        }

        return presenter.Present(target, width, height);
    }

    public void ReleaseNativeSurface(IntPtr window) {
        if (!_presenters.Remove(window, out var presenter)) {
            return;
        }

        presenter.Dispose();
    }

    public void MakeCurrent() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!WglNative.wglMakeCurrent(_deviceContext, _context)) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "wglMakeCurrent failed.");
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        try {
            foreach (var presenter in _presenters.Values) {
                SkiaCleanup.Capture(ref failure, presenter.Dispose);
            }
        } finally {
            _presenters.Clear();
        }

        SkiaCleanup.Capture(ref failure, GpuContext.Dispose);
        if (_glInterface is not null) {
            SkiaCleanup.Capture(ref failure, _glInterface.Dispose);
            _glInterface = null;
        }

        SkiaCleanup.Capture(
            ref failure,
            () => _ = WglNative.wglMakeCurrent(
                IntPtr.Zero,
                IntPtr.Zero));
        SkiaCleanup.Capture(
            ref failure,
            () => _ = WglNative.wglDeleteContext(_context));
        if (_deviceContext != IntPtr.Zero) {
            SkiaCleanup.Capture(
                ref failure,
                () => _ = WglNative.ReleaseDC(
                    _window,
                    _deviceContext));
        }

        if (_window != IntPtr.Zero) {
            SkiaCleanup.Capture(
                ref failure,
                () => _ = WglNative.DestroyWindow(_window));
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private static void EnsureWindowClass() {
        if (_className is not null) {
            return;
        }

        lock (RegistrationLock) {
            if (_className is not null) {
                return;
            }

            _instance = WglNative.GetModuleHandleW(null);
            if (_instance == IntPtr.Zero) {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "GetModuleHandleW failed.");
            }

            var className = $"Melanopsin.Graphics.Backend.SkiaGPU.{Environment.ProcessId}";
            var windowClass = new WndClassEx {
                Size = (uint)Marshal.SizeOf<WndClassEx>(),
                Instance = _instance,
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(
                    WindowProcedure),
                Background = new IntPtr(1),
                ClassName = className,
            };
            var atom = WglNative.RegisterClassExW(ref windowClass);
            if (atom == 0) {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410) {
                    throw new Win32Exception(error, "RegisterClassExW failed.");
                }
            }

            _className = className;
        }
    }

    private void DisposeHandles() {
        if (_deviceContext != IntPtr.Zero) {
            _ = WglNative.ReleaseDC(_window, _deviceContext);
        }

        if (_window != IntPtr.Zero) {
            _ = WglNative.DestroyWindow(_window);
        }
    }

    private static T LoadProcedure<T>(params string[] names)
        where T : Delegate {
        foreach (var name in names) {
            var address = WglNative.wglGetProcAddress(name);
            if (address != IntPtr.Zero) {
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
        }

        throw new InvalidOperationException(
            $"OpenGL procedure '{names[0]}' is unavailable.");
    }

    internal IntPtr ContextHandle => _context;
}
