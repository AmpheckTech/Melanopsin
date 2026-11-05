// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.UI.OpenGL;

internal static unsafe partial class EglNative {
    internal const string EglLibrary = "libEGL.so.1";
    internal const string WaylandLibrary = "libwayland-client.so.0";

    internal const uint EglOpenGlApi = 0x30A2;
    internal const uint EglPlatformWayland = 0x31D8;
    internal const uint EglPlatformSurfaceless = 0x31DD;

    internal const int EglSurfaceType = 0x3033;
    internal const int EglPbufferBit = 0x0001;
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr GetPlatformDisplay(
        uint platform,
        IntPtr nativeDisplay,
        IntPtr attributes);

    [LibraryImport(EglLibrary, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr eglGetProcAddress(string procedureName);

    [LibraryImport(EglLibrary)]
    internal static partial int eglInitialize(
        IntPtr display,
        out int majorVersion,
        out int minorVersion);

    [LibraryImport(EglLibrary)]
    internal static partial int eglTerminate(IntPtr display);

    [LibraryImport(EglLibrary)]
    internal static partial int eglGetError();

    [LibraryImport(EglLibrary)]
    internal static partial int eglBindAPI(uint api);

    [LibraryImport(EglLibrary)]
    internal static partial int eglChooseConfig(
        IntPtr display,
        int* attributes,
        out IntPtr configuration,
        int configurationSize,
        out int configurationCount);

    [LibraryImport(EglLibrary)]
    internal static partial IntPtr eglCreatePbufferSurface(
        IntPtr display,
        IntPtr configuration,
        int* attributes);

    [LibraryImport(EglLibrary)]
    internal static partial int eglDestroySurface(IntPtr display, IntPtr surface);

    [LibraryImport(EglLibrary)]
    internal static partial IntPtr eglCreateContext(
        IntPtr display,
        IntPtr configuration,
        IntPtr shareContext,
        int* attributes);

    [LibraryImport(EglLibrary)]
    internal static partial int eglDestroyContext(IntPtr display, IntPtr context);

    [LibraryImport(EglLibrary)]
    internal static partial int eglMakeCurrent(
        IntPtr display,
        IntPtr drawSurface,
        IntPtr readSurface,
        IntPtr context);

    [LibraryImport(WaylandLibrary)]
    internal static partial IntPtr wl_display_connect(IntPtr name);

    [LibraryImport(WaylandLibrary)]
    internal static partial void wl_display_disconnect(IntPtr display);
}
