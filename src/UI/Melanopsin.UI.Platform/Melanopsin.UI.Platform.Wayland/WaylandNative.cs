// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal static unsafe partial class WaylandNative {
    internal const string LibraryName = "libMelanopsinWayland.so";

    [StructLayout(LayoutKind.Sequential)]
    internal struct WaylandCallbacks {
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, int, int, double, void> Configure;
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, void> Close;
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, uint, double, double, uint, uint, void> Pointer;
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, double, double, int, int, uint, void> Axis;
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, uint, uint, uint, byte*, uint, void> Key;
        internal delegate* unmanaged[Cdecl]<IntPtr, nuint, int, void> Focus;
        internal delegate* unmanaged[Cdecl]<IntPtr, byte*, void> Error;
    }

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_host_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr HostCreate(
        ref WaylandCallbacks callbacks,
        IntPtr context,
        string? displayName);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_host_destroy")]
    internal static partial void HostDestroy(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_host_get_wl_display")]
    internal static partial IntPtr HostGetWlDisplay(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nuint WindowCreate(
        IntPtr host,
        ulong windowId,
        string title,
        int logicalWidth,
        int logicalHeight);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_destroy")]
    internal static partial void WindowDestroy(IntPtr host, nuint window);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_get_wl_surface")]
    internal static partial IntPtr WindowGetWlSurface(nuint window);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_begin_egl_present")]
    internal static partial int WindowBeginEglPresent(
        IntPtr host,
        nuint window,
        int width,
        int height);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_before_egl_swap")]
    internal static partial int WindowBeforeEglSwap(
        IntPtr host,
        nuint window);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_cancel_egl_present")]
    internal static partial void WindowCancelEglPresent(
        IntPtr host,
        nuint window);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_can_present")]
    internal static partial int WindowCanPresent(
        IntPtr host,
        nuint window,
        int width,
        int height);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_present")]
    internal static partial int WindowPresent(
        IntPtr host,
        nuint window,
        byte* pixels,
        int width,
        int height,
        int stride,
        int logicalWidth,
        int logicalHeight);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_request_close")]
    internal static partial void WindowRequestClose(IntPtr host, nuint window);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_window_set_title", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowSetTitle(
        IntPtr host,
        nuint window,
        string title);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_dispatch_pending")]
    internal static partial int DispatchPending(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_wait_for_event")]
    internal static partial int WaitForEvent(IntPtr host, int timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_flush")]
    internal static partial int Flush(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_display_scale")]
    internal static partial double DisplayScale(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_last_error")]
    internal static partial IntPtr LastError(IntPtr host);

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_last_global_error")]
    internal static partial IntPtr LastGlobalError();

    [LibraryImport(LibraryName, EntryPoint = "ml_wayland_clear_error")]
    internal static partial void ClearError(IntPtr host);
}
