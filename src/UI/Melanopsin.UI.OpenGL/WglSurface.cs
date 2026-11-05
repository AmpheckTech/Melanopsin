// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.OpenGL;

internal sealed class WglSurface : IOpenGLSurface {
    private readonly IntPtr _window;
    private readonly IntPtr _deviceContext;
    private readonly IntPtr _context;
    private byte[] _readback = [];
    private byte[] _pixels = [];
    private int _width;
    private int _height;
    private bool _disposed;

    public WglSurface() {
        _window = WglWindowClass.CreateWindow();
        _deviceContext = OpenGLNative.GetDC(_window);
        if (_deviceContext == IntPtr.Zero) {
            OpenGLNative.DestroyWindow(_window);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "GetDC failed.");
        }

        var format = new PixelFormatDescriptor {
            Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
            Version = 1,
            Flags = OpenGLNative.PfdDrawToWindow |
                OpenGLNative.PfdSupportOpenGL |
                OpenGLNative.PfdDoubleBuffer,
            PixelType = OpenGLNative.PfdTypeRgba,
            ColorBits = 32,
            RedBits = 8,
            GreenBits = 8,
            BlueBits = 8,
            AlphaBits = 8,
            DepthBits = 24,
        };
        var pixelFormat = OpenGLNative.ChoosePixelFormat(_deviceContext, ref format);
        if (pixelFormat == 0 ||
            !OpenGLNative.SetPixelFormat(_deviceContext, pixelFormat, ref format)) {
            OpenGLNative.ReleaseDC(_window, _deviceContext);
            OpenGLNative.DestroyWindow(_window);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not configure an OpenGL pixel format.");
        }

        _context = OpenGLNative.wglCreateContext(_deviceContext);
        if (_context == IntPtr.Zero) {
            OpenGLNative.ReleaseDC(_window, _deviceContext);
            OpenGLNative.DestroyWindow(_window);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "wglCreateContext failed.");
        }

        MakeCurrent();
    }

    public ReadOnlyMemory<byte> Pixels => _pixels;

    public int Stride => _width * 4;

    public ReadOnlyMemory<byte> Render(
        int width,
        int height,
        Color clearColor,
        Action<int, int>? content) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureSize(width, height);
        MakeCurrent();
        return OpenGLFrameRenderer.Render(
            width,
            height,
            clearColor,
            content,
            readBuffer: OpenGLNative.GlBack,
            ref _readback,
            ref _pixels);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _ = OpenGLNative.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        _ = OpenGLNative.wglDeleteContext(_context);
        _ = OpenGLNative.ReleaseDC(_window, _deviceContext);
        _ = OpenGLNative.DestroyWindow(_window);
    }

    private void EnsureSize(int width, int height) {
        if (_width == width && _height == height) {
            return;
        }

        if (!OpenGLNative.MoveWindow(_window, 0, 0, width, height, repaint: false)) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not resize the OpenGL surface.");
        }

        _width = width;
        _height = height;
    }

    private void MakeCurrent() {
        if (!OpenGLNative.wglMakeCurrent(_deviceContext, _context)) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "wglMakeCurrent failed.");
        }
    }

}
