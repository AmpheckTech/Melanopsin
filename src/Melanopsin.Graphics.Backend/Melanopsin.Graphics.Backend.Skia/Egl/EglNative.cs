// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.Graphics.Backend.Skia;

internal static class EglNative {
    internal const uint EglOpenGlApi = 0x30A2;
    internal const uint EglPlatformWayland = 0x31D8;
    internal const uint EglPlatformSurfaceless = 0x31DD;

    internal const int EglSurfaceType = 0x3033;
    internal const int EglPbufferBit = 0x0001;
    internal const int EglWindowBit = 0x0004;
    internal const int EglRenderableType = 0x3040;
    internal const int EglOpenGlBit = 0x0008;
    internal const int EglRedSize = 0x3024;
    internal const int EglGreenSize = 0x3023;
    internal const int EglBlueSize = 0x3022;
    internal const int EglAlphaSize = 0x3021;
    internal const int EglDepthSize = 0x3025;
    internal const int EglWidth = 0x3057;
    internal const int EglHeight = 0x3056;
    internal const int EglNone = 0x3038;

    internal const uint GlTexture2D = 0x0DE1;
    internal const uint GlBack = 0x0405;
    internal const uint GlReadFramebuffer = 0x8CA8;
    internal const uint GlDrawFramebuffer = 0x8CA9;
    internal const uint GlColorBufferBit = 0x00004000;
    internal const uint GlNearest = 0x2600;
    internal const uint GlDepthTest = 0x0B71;
    internal const uint GlBlend = 0x0BE2;
    internal const uint GlScissorTest = 0x0C11;
    internal const uint GlCullFace = 0x0B44;
    internal const uint GlRgba8 = 0x8058;
    internal const uint GlBgra = 0x80E1;
    internal const uint GlUnsignedByte = 0x1401;
    internal const uint GlTextureMinFilter = 0x2801;
    internal const uint GlTextureMagFilter = 0x2800;
    internal const uint GlLinear = 0x2601;
    internal const uint GlFramebuffer = 0x8D40;
    internal const uint GlColorAttachment0 = 0x8CE0;
    internal const uint GlFramebufferComplete = 0x8CD5;
    internal const uint GlNoError = 0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr GetPlatformDisplay(
        uint platform,
        IntPtr nativeDisplay,
        IntPtr attributes);

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi)]
    internal static extern IntPtr eglGetProcAddress(string procedureName);

    [DllImport("libEGL.so.1")]
    internal static extern IntPtr eglGetDisplay(IntPtr nativeDisplay);

    [DllImport("libEGL.so.1")]
    internal static extern int eglInitialize(
        IntPtr display,
        out int majorVersion,
        out int minorVersion);

    [DllImport("libEGL.so.1")]
    internal static extern int eglTerminate(IntPtr display);

    [DllImport("libEGL.so.1")]
    internal static extern int eglGetError();

    [DllImport("libEGL.so.1")]
    internal static extern int eglBindAPI(uint api);

    [DllImport("libEGL.so.1")]
    internal static extern int eglChooseConfig(
        IntPtr display,
        int[] attributes,
        out IntPtr configuration,
        int configurationSize,
        out int configurationCount);

    [DllImport("libEGL.so.1")]
    internal static extern IntPtr eglCreateContext(
        IntPtr display,
        IntPtr configuration,
        IntPtr shareContext,
        int[] attributes);

    [DllImport("libEGL.so.1")]
    internal static extern int eglDestroyContext(
        IntPtr display,
        IntPtr context);

    [DllImport("libEGL.so.1")]
    internal static extern IntPtr eglCreatePbufferSurface(
        IntPtr display,
        IntPtr configuration,
        int[] attributes);

    [DllImport("libEGL.so.1")]
    internal static extern IntPtr eglCreateWindowSurface(
        IntPtr display,
        IntPtr configuration,
        IntPtr nativeWindow,
        int[] attributes);

    [DllImport("libEGL.so.1")]
    internal static extern int eglDestroySurface(
        IntPtr display,
        IntPtr surface);

    [DllImport("libEGL.so.1")]
    internal static extern int eglMakeCurrent(
        IntPtr display,
        IntPtr drawSurface,
        IntPtr readSurface,
        IntPtr context);

    [DllImport("libEGL.so.1")]
    internal static extern int eglSwapInterval(IntPtr display, int interval);

    [DllImport("libEGL.so.1")]
    internal static extern int eglSwapBuffers(IntPtr display, IntPtr surface);

    [DllImport("libwayland-client.so.0")]
    internal static extern IntPtr wl_display_connect(IntPtr name);

    [DllImport("libwayland-client.so.0")]
    internal static extern void wl_display_disconnect(IntPtr display);

    [DllImport("libwayland-egl.so.1")]
    internal static extern IntPtr wl_egl_window_create(
        IntPtr surface,
        int width,
        int height);

    [DllImport("libwayland-egl.so.1")]
    internal static extern void wl_egl_window_resize(
        IntPtr eglWindow,
        int width,
        int height,
        int dx,
        int dy);

    [DllImport("libwayland-egl.so.1")]
    internal static extern void wl_egl_window_destroy(IntPtr eglWindow);

    [DllImport("libGL.so.1")]
    internal static extern void glViewport(int x, int y, int width, int height);

    [DllImport("libGL.so.1")]
    internal static extern uint glGetError();

    [DllImport("libGL.so.1")]
    internal static extern void glReadBuffer(uint mode);

    [DllImport("libGL.so.1")]
    internal static extern void glDrawBuffer(uint mode);

    [DllImport("libGL.so.1")]
    internal static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport("libGL.so.1")]
    internal static extern void glClear(uint mask);

    [DllImport("libGL.so.1")]
    internal static extern void glColorMask(
        byte red,
        byte green,
        byte blue,
        byte alpha);

    [DllImport("libGL.so.1")]
    internal static extern void glEnable(uint capability);

    [DllImport("libGL.so.1")]
    internal static extern void glDisable(uint capability);

    [DllImport("libGL.so.1")]
    internal static extern void glGenTextures(int count, out uint textures);

    [DllImport("libGL.so.1")]
    internal static extern void glDeleteTextures(int count, ref uint textures);

    [DllImport("libGL.so.1")]
    internal static extern void glBindTexture(uint target, uint texture);

    [DllImport("libGL.so.1")]
    internal static extern void glTexParameteri(
        uint target,
        uint name,
        int value);

    [DllImport("libGL.so.1")]
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

    [DllImport("libGL.so.1")]
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

    [DllImport("libGL.so.1")]
    internal static extern void glGenFramebuffers(
        int count,
        out uint framebuffers);

    [DllImport("libGL.so.1")]
    internal static extern void glDeleteFramebuffers(
        int count,
        ref uint framebuffers);

    [DllImport("libGL.so.1")]
    internal static extern void glBindFramebuffer(
        uint target,
        uint framebuffer);

    [DllImport("libGL.so.1")]
    internal static extern void glBlitFramebuffer(
        int sourceX0,
        int sourceY0,
        int sourceX1,
        int sourceY1,
        int destinationX0,
        int destinationY0,
        int destinationX1,
        int destinationY1,
        uint mask,
        uint filter);

    [DllImport("libGL.so.1")]
    internal static extern void glFramebufferTexture2D(
        uint target,
        uint attachment,
        uint textureTarget,
        uint texture,
        int level);

    [DllImport("libGL.so.1")]
    internal static extern uint glCheckFramebufferStatus(uint target);
}
