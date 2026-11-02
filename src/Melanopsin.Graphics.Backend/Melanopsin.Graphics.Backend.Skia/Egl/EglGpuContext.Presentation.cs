namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class EglGpuContext {
    private sealed class EglPresentationSurface : IDisposable {
        private readonly EglGpuContext _owner;
        private readonly IntPtr _nativeSurface;
        private IntPtr _eglWindow;
        private IntPtr _eglSurface;
        private int _width;
        private int _height;
        private bool _disposed;

        private EglPresentationSurface(
            EglGpuContext owner,
            IntPtr nativeSurface,
            IntPtr eglWindow,
            IntPtr eglSurface,
            int width,
            int height) {
            _owner = owner;
            _nativeSurface = nativeSurface;
            _eglWindow = eglWindow;
            _eglSurface = eglSurface;
            _width = width;
            _height = height;
        }

        public static EglPresentationSurface? Create(
            EglGpuContext owner,
            IntPtr nativeSurface,
            int width,
            int height) {
            var eglWindow = EglNative.wl_egl_window_create(
                nativeSurface,
                width,
                height);
            if (eglWindow == IntPtr.Zero) {
                return null;
            }

            var eglSurface = EglNative.eglCreateWindowSurface(
                owner._display,
                owner._configuration,
                eglWindow,
                [EglNative.EglNone]);
            if (eglSurface == IntPtr.Zero) {
                EglNative.wl_egl_window_destroy(eglWindow);
                return null;
            }

            return new EglPresentationSurface(
                owner,
                nativeSurface,
                eglWindow,
                eglSurface,
                width,
                height);
        }

        public bool Present(
            EglGpuContext owner,
            SkiaGpuRenderTargetHandle target,
            int width,
            int height) {
            if (_disposed || owner != _owner || target.TextureId == 0 ||
                width <= 0 || height <= 0 ||
                width > target.Width || height > target.Height) {
                return false;
            }

            EnsureSize(width, height);

            if (!owner.TryMakeCurrent(_eglSurface)) {
                return false;
            }

            try {
                // Some Mesa versions reset the swap interval after a resize or
                // surface reconfiguration. Re-assert zero to avoid adding a
                // second compositor-level presentation delay.
                _ = EglNative.eglSwapInterval(owner._display, 0);
                if (!DrawTexture(target, width, height)) {
                    return false;
                }

                return EglNative.eglSwapBuffers(
                    owner._display,
                    _eglSurface) != 0;
            } finally {
                _ = owner.TryMakeCurrent(owner._surface);
            }
        }

        private void EnsureSize(int width, int height) {
            if (width == _width && height == _height) {
                return;
            }

            EglNative.wl_egl_window_resize(
                _eglWindow,
                width,
                height,
                dx: 0,
                dy: 0);
            _width = width;
            _height = height;
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            if (_eglSurface != IntPtr.Zero && _owner._display != IntPtr.Zero) {
                _ = EglNative.eglDestroySurface(
                    _owner._display,
                    _eglSurface);
            }

            if (_eglWindow != IntPtr.Zero) {
                EglNative.wl_egl_window_destroy(_eglWindow);
            }

            _eglSurface = IntPtr.Zero;
            _eglWindow = IntPtr.Zero;
        }

        private static bool DrawTexture(
            SkiaGpuRenderTargetHandle target,
            int width,
            int height) {
            while (EglNative.glGetError() != EglNative.GlNoError) {
            }

            EglNative.glDisable(EglNative.GlDepthTest);
            EglNative.glDisable(EglNative.GlBlend);
            EglNative.glDisable(EglNative.GlScissorTest);
            EglNative.glDisable(EglNative.GlCullFace);
            // Skia can leave an offscreen framebuffer bound as the read
            // framebuffer. Presentation only needs the default draw
            // framebuffer; changing GL_READ_BUFFER here is invalid while the
            // offscreen read framebuffer is still bound on Mesa.
            EglNative.glBindFramebuffer(
                EglNative.GlDrawFramebuffer,
                0);
            SelectBackBuffer();
            EglNative.glViewport(0, 0, width, height);
            EglNative.glClearColor(0, 0, 0, 1);
            EglNative.glClear(EglNative.GlColorBufferBit);
            // Wayland top-level windows in Melanopsin are opaque. Preserve the
            // alpha written by the clear instead of copying the offscreen
            // surface's alpha into the window back buffer. Otherwise resize
            // frames can briefly become translucent or black where Skia has
            // not painted an opaque pixel.
            EglNative.glColorMask(1, 1, 1, 0);
            try {
                EglNative.glBindFramebuffer(
                    EglNative.GlReadFramebuffer,
                    target.FramebufferId);
                EglNative.glReadBuffer(EglNative.GlColorAttachment0);
                EglNative.glBlitFramebuffer(
                    sourceX0: 0,
                    // Skia's FBO stores the first canvas row at the bottom of
                    // the GL texture. Flip while copying into the Wayland back
                    // buffer; otherwise the presented window is upside down.
                    sourceY0: height,
                    sourceX1: width,
                    sourceY1: 0,
                    destinationX0: 0,
                    destinationY0: 0,
                    destinationX1: width,
                    destinationY1: height,
                    EglNative.GlColorBufferBit,
                    EglNative.GlNearest);
            } finally {
                EglNative.glColorMask(1, 1, 1, 1);
                EglNative.glBindFramebuffer(EglNative.GlFramebuffer, 0);
            }

            return EglNative.glGetError() == EglNative.GlNoError;
        }

        private static void SelectBackBuffer() {
            // NVIDIA's EGL Wayland implementation can incorrectly keep the
            // default draw buffer on GL_FRONT after a surface resize.
            // Explicitly selecting GL_BACK avoids presenting a stale or empty
            // front buffer while the compositor is reconfiguring the window.
            EglNative.glDrawBuffer(EglNative.GlBack);
        }
    }
}
