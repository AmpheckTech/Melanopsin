// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.Graphics.Backend.Skia;

internal static class WglNative {
    internal const uint WsPopup = 0x80000000;
    internal const uint PfdDrawToWindow = 0x00000004;
    internal const uint PfdSupportOpenGL = 0x00000020;
    internal const uint PfdDoubleBuffer = 0x00000001;
    internal const byte PfdTypeRgba = 0;
    internal const uint GlTexture2D = 0x0DE1;
    internal const uint GlRgba8 = 0x8058;
    internal const uint GlBgra = 0x80E1;
    internal const uint GlUnsignedByte = 0x1401;
    internal const uint GlTextureMinFilter = 0x2801;
    internal const uint GlTextureMagFilter = 0x2800;
    internal const uint GlLinear = 0x2601;
    internal const uint GlFramebuffer = 0x8D40;
    internal const uint GlColorAttachment0 = 0x8CE0;
    internal const uint GlFramebufferComplete = 0x8CD5;
    internal const uint GlColorBufferBit = 0x4000;
    internal const uint GlProjection = 0x1701;
    internal const uint GlModelView = 0x1700;
    internal const uint GlQuads = 0x0007;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProcW(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int ChoosePixelFormat(
        IntPtr deviceContext,
        ref PixelFormatDescriptor format);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool SetPixelFormat(
        IntPtr deviceContext,
        int format,
        ref PixelFormatDescriptor descriptor);

    [DllImport("opengl32.dll", SetLastError = true)]
    internal static extern IntPtr wglCreateContext(IntPtr deviceContext);

    [DllImport("opengl32.dll", SetLastError = true)]
    internal static extern bool wglMakeCurrent(
        IntPtr deviceContext,
        IntPtr context);

    [DllImport("opengl32.dll", SetLastError = true)]
    internal static extern bool wglDeleteContext(IntPtr context);

    [DllImport("opengl32.dll", SetLastError = true)]
    internal static extern bool wglShareLists(IntPtr first, IntPtr second);

    [DllImport("opengl32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern IntPtr wglGetProcAddress(string name);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool SwapBuffers(IntPtr deviceContext);

    [DllImport("opengl32.dll")]
    internal static extern void glGenTextures(int count, out uint textures);

    [DllImport("opengl32.dll")]
    internal static extern void glDeleteTextures(int count, ref uint textures);

    [DllImport("opengl32.dll")]
    internal static extern void glBindTexture(uint target, uint texture);

    [DllImport("opengl32.dll")]
    internal static extern void glTexParameteri(uint target, uint name, int value);

    [DllImport("opengl32.dll")]
    internal static extern void glTexImage2D(
        uint target,
        int level,
        int internalFormat,
        int width,
        int height,
        int border,
        uint format,
        uint type,
        IntPtr pixels);

    [DllImport("opengl32.dll")]
    internal static extern void glTexSubImage2D(
        uint target,
        int level,
        int xOffset,
        int yOffset,
        int width,
        int height,
        uint format,
        uint type,
        IntPtr pixels);

    [DllImport("opengl32.dll")]
    internal static extern void glViewport(int x, int y, int width, int height);

    [DllImport("opengl32.dll")]
    internal static extern void glClearColor(
        float red,
        float green,
        float blue,
        float alpha);

    [DllImport("opengl32.dll")]
    internal static extern void glClear(uint mask);

    [DllImport("opengl32.dll")]
    internal static extern void glMatrixMode(uint mode);

    [DllImport("opengl32.dll")]
    internal static extern void glLoadIdentity();

    [DllImport("opengl32.dll")]
    internal static extern void glOrtho(
        double left,
        double right,
        double bottom,
        double top,
        double near,
        double far);

    [DllImport("opengl32.dll")]
    internal static extern void glBegin(uint mode);

    [DllImport("opengl32.dll")]
    internal static extern void glEnd();

    [DllImport("opengl32.dll")]
    internal static extern void glTexCoord2f(float x, float y);

    [DllImport("opengl32.dll")]
    internal static extern void glVertex2f(float x, float y);

    [DllImport("opengl32.dll")]
    internal static extern void glEnable(uint capability);

    [DllImport("opengl32.dll")]
    internal static extern void glDisable(uint capability);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WindowProcedure(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void GenFramebuffers(int count, out uint framebuffers);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void DeleteFramebuffers(
        int count,
        ref uint framebuffers);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void BindFramebuffer(uint target, uint framebuffer);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void FramebufferTexture2D(
        uint target,
        uint attachment,
        uint textureTarget,
        uint texture,
        int level);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate uint CheckFramebufferStatus(uint target);
}
