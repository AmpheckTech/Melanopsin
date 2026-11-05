// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.OpenGL;

internal static class OpenGLNative {
    private const string OpenGLLibrary = "melanopsin-opengl";

    internal const uint WsPopup = 0x80000000;
    internal const uint PfdDrawToWindow = 0x00000004;
    internal const uint PfdSupportOpenGL = 0x00000020;
    internal const uint PfdDoubleBuffer = 0x00000001;
    internal const byte PfdTypeRgba = 0;

    internal const uint GlColorBufferBit = 0x00004000;
    internal const uint GlDepthTest = 0x00000B71;
    internal const uint GlFront = 0x0404;
    internal const uint GlBack = 0x0405;
    internal const uint GlBgra = 0x80E1;
    internal const uint GlUnsignedByte = 0x1401;
    internal const uint GlPackAlignment = 0x0D05;
    internal const uint GlProjection = 0x1701;
    internal const uint GlModelView = 0x1700;
    internal const uint GlTriangles = 0x0004;
    internal const uint GlLines = 0x0001;

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

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(
        IntPtr window,
        int x,
        int y,
        int width,
        int height,
        bool repaint);

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
    internal static extern bool wglMakeCurrent(IntPtr deviceContext, IntPtr context);

    [DllImport("opengl32.dll", SetLastError = true)]
    internal static extern bool wglDeleteContext(IntPtr context);

    static OpenGLNative() {
        NativeLibrary.SetDllImportResolver(
            typeof(OpenGLNative).Assembly,
            ResolveOpenGLLibrary);
    }

    [DllImport(OpenGLLibrary)]
    internal static extern void glViewport(int x, int y, int width, int height);

    [DllImport(OpenGLLibrary)]
    internal static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport(OpenGLLibrary)]
    internal static extern void glClear(uint mask);

    [DllImport(OpenGLLibrary)]
    internal static extern void glDisable(uint capability);

    [DllImport(OpenGLLibrary)]
    internal static extern void glMatrixMode(uint mode);

    [DllImport(OpenGLLibrary)]
    internal static extern void glLoadIdentity();

    [DllImport(OpenGLLibrary)]
    internal static extern void glOrtho(
        double left,
        double right,
        double bottom,
        double top,
        double near,
        double far);

    [DllImport(OpenGLLibrary)]
    internal static extern void glBegin(uint mode);

    [DllImport(OpenGLLibrary)]
    internal static extern void glEnd();

    [DllImport(OpenGLLibrary)]
    internal static extern void glColor4f(float red, float green, float blue, float alpha);

    [DllImport(OpenGLLibrary)]
    internal static extern void glVertex2f(float x, float y);

    [DllImport(OpenGLLibrary)]
    internal static extern void glFinish();

    [DllImport(OpenGLLibrary)]
    internal static extern void glReadBuffer(uint mode);

    [DllImport(OpenGLLibrary)]
    internal static extern void glPixelStorei(uint parameter, int value);

    [DllImport(OpenGLLibrary)]
    internal static extern void glReadPixels(
        int x,
        int y,
        int width,
        int height,
        uint format,
        uint type,
        IntPtr pixels);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WindowProcedure(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    private static IntPtr ResolveOpenGLLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath) {
        if (!string.Equals(
                libraryName,
                OpenGLLibrary,
                StringComparison.Ordinal)) {
            return IntPtr.Zero;
        }

        var platformLibrary = OperatingSystem.IsWindows()
            ? "opengl32.dll"
            : OperatingSystem.IsLinux()
                ? "libGL.so.1"
                : null;
        return platformLibrary is null
            ? IntPtr.Zero
            : NativeLibrary.Load(platformLibrary, assembly, searchPath);
    }
}
