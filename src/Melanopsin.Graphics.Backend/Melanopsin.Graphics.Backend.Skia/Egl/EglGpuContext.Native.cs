// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class EglGpuContext {
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
