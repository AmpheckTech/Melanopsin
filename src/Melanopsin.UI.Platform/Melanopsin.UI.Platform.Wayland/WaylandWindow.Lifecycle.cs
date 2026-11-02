namespace Melanopsin.UI.Platform.Wayland;

internal sealed partial class WaylandWindow {
    private bool _disposed;

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _frameCallback.Dispose();
        _bufferPool.Dispose();
        if (GpuSurface is { } gpuSurface) {
            gpuSurface.ReleasePresentSurface(_surface.Handle);
            GpuSurface = null;
        }

        if (_viewport is not null) {
            if (!_viewport.IsDisposed) {
                _viewport.Destroy();
                _viewport.Dispose();
            }

            _viewport = null;
        }

        if (_fractionalScale is not null) {
            if (!_fractionalScale.IsDisposed) {
                _fractionalScale.Destroy();
                _fractionalScale.Dispose();
            }

            _fractionalScale = null;
        }

        Decoration?.Dispose();
        Decoration = null;
        if (!_surface.IsDisposed) {
            _surface.Destroy();
            _surface.Dispose();
        }
    }
}
