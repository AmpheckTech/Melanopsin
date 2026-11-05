// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class WglPresentationSurface : IDisposable {
    private readonly IntPtr _window;
    private readonly IntPtr _deviceContext;
    private readonly IntPtr _context;
    private bool _disposed;

    public WglPresentationSurface(
        WglContext sharedContext,
        IntPtr window) {
        _window = window;
        _deviceContext = WglNative.GetDC(window);
        if (_deviceContext == IntPtr.Zero) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not acquire the presentation device context.");
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
            _ = WglNative.ReleaseDC(window, _deviceContext);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not configure the presentation pixel format.");
        }

        _context = WglNative.wglCreateContext(_deviceContext);
        if (_context == IntPtr.Zero ||
            !WglNative.wglShareLists(sharedContext.ContextHandle, _context)) {
            if (_context != IntPtr.Zero) {
                _ = WglNative.wglDeleteContext(_context);
            }

            _ = WglNative.ReleaseDC(window, _deviceContext);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not create a shared presentation GL context.");
        }
    }

    public bool Present(
        SkiaGpuRenderTargetHandle target,
        int width,
        int height) {
        if (_disposed ||
            target.TextureId == 0 ||
            width <= 0 || height <= 0 ||
            width > target.Width || height > target.Height ||
            !WglNative.wglMakeCurrent(_deviceContext, _context)) {
            return false;
        }

        var u = (float)width / target.Width;
        var v = (float)height / target.Height;
        WglNative.glViewport(0, 0, width, height);
        WglNative.glClearColor(0, 0, 0, 1);
        WglNative.glClear(WglNative.GlColorBufferBit);
        WglNative.glMatrixMode(WglNative.GlProjection);
        WglNative.glLoadIdentity();
        WglNative.glOrtho(0, width, height, 0, -1, 1);
        WglNative.glMatrixMode(WglNative.GlModelView);
        WglNative.glLoadIdentity();
        WglNative.glEnable(WglNative.GlTexture2D);
        WglNative.glBindTexture(WglNative.GlTexture2D, target.TextureId);
        WglNative.glBegin(WglNative.GlQuads);
        WglNative.glTexCoord2f(0, 0);
        WglNative.glVertex2f(0, 0);
        WglNative.glTexCoord2f(u, 0);
        WglNative.glVertex2f(width, 0);
        WglNative.glTexCoord2f(u, v);
        WglNative.glVertex2f(width, height);
        WglNative.glTexCoord2f(0, v);
        WglNative.glVertex2f(0, height);
        WglNative.glEnd();
        WglNative.glDisable(WglNative.GlTexture2D);
        return WglNative.SwapBuffers(_deviceContext);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        _ = WglNative.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        SkiaCleanup.Capture(
            ref failure,
            () => _ = WglNative.wglDeleteContext(_context));
        SkiaCleanup.Capture(
            ref failure,
            () => _ = WglNative.ReleaseDC(_window, _deviceContext));
        SkiaCleanup.ThrowIfFailed(failure);
    }
}
