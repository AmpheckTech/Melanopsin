using NWayland;
using NWayland.Protocols.FractionalScaleV1;
using NWayland.Protocols.Viewporter;
using NWayland.Protocols.Wayland;
using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandWindow : IDisposable {
    private readonly WlCompositor _compositor;
    private readonly WlSurface _surface;
    private readonly IWlTargetQueue _queue;
    private readonly WaylandFrameCallback _frameCallback = new();
    private readonly WaylandShmBufferPool _bufferPool;
    private readonly SurfaceListener _surfaceListener;
    private readonly Action<WaylandWindow, bool, bool, bool> _onConfigured;
    private readonly Action<WaylandWindow> _onCloseRequested;
    private readonly Action _onBufferReleased;
    private WpFractionalScaleV1? _fractionalScale;
    private WpViewport? _viewport;
    private PixelSize? _opaqueRegionSize;
    private int _configuredBufferScale;
    private int _configuredSourceWidth;
    private int _configuredSourceHeight;
    private int _configuredDestinationWidth;
    private int _configuredDestinationHeight;
    private bool _hasSurfaceContents;
    private bool _disposed;

    public WaylandWindow(
        WindowId id,
        WaylandConnection connection,
        string title,
        PixelSize initialPhysicalSize,
        PixelSize initialLogicalSize,
        float displayScale,
        Action<WaylandWindow, bool, bool, bool> onConfigured,
        Action<WaylandWindow> onCloseRequested,
        Action onBufferReleased) {
        Id = id;
        _queue = connection.Queue;
        _onConfigured = onConfigured;
        _onCloseRequested = onCloseRequested;
        _onBufferReleased = onBufferReleased;
        Title = title;
        LogicalSize = initialLogicalSize;
        Size = initialPhysicalSize;
        DisplayScale = NormalizeScale(displayScale);
        _compositor = connection.Compositor ??
            throw new ObjectDisposedException(nameof(WaylandWindow));
        var shm = connection.Shm ??
            throw new ObjectDisposedException(nameof(WaylandWindow));
        _surfaceListener = new SurfaceListener(this);
        _surface = _compositor.CreateSurface(
            _surfaceListener,
            connection.Queue);
        _bufferPool = new WaylandShmBufferPool(
            shm,
            connection.Queue,
            onBufferReleased);

        if (_compositor.IsDisposed || shm.IsDisposed) {
            throw new ObjectDisposedException(nameof(WaylandWindow));
        }

        if (connection.FractionalScaleManager is { } fractionalScaleManager &&
            connection.Viewporter is { } viewporter) {
            _fractionalScale = fractionalScaleManager.GetFractionalScale(
                _surface,
                new FractionalScaleListener(this),
                connection.Queue);
            _viewport = viewporter.GetViewport(
                _surface,
                null,
                null);
        }
    }

    public WindowId Id { get; }

    public IGpuFrameSurface? GpuSurface { get; set; }

    public IntPtr Handle => _surface.Handle;

    public WlSurface Surface => _surface;

    public WaylandDecoration? Decoration { get; private set; }

    public string Title { get; private set; }

    public PixelSize LogicalSize { get; private set; }

    public PixelSize Size { get; private set; }

    public float DisplayScale { get; private set; }

    public float PointerX { get; set; }

    public float PointerY { get; set; }

    public bool IsFocused { get; set; }

    public bool IsShown { get; set; }

    public bool IsConfigured { get; private set; }

    public bool HasFractionalScaling => _fractionalScale is not null && _viewport is not null;

    public bool IsFrameCallbackPending => _frameCallback.IsPending;

    public bool CanPresent(PixelSize physicalSize) {
        return !_disposed &&
            IsConfigured &&
            !_frameCallback.IsPending &&
            physicalSize == Size;
    }

    public void AttachDecoration(WaylandDecoration decoration) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Decoration = decoration;
    }

    public void SetTitle(string title) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Title = title;
        Decoration?.SetTitle(title);
    }

    public bool UpdateConfiguration(
        PixelSize logicalSize,
        float displayScale,
        out bool scaleChanged,
        out bool sizeChanged) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        logicalSize = new PixelSize(
            Math.Max(1, logicalSize.Width),
            Math.Max(1, logicalSize.Height));
        displayScale = NormalizeScale(displayScale);
        scaleChanged = Math.Abs(DisplayScale - displayScale) >= 0.0001f;
        DisplayScale = displayScale;
        LogicalSize = logicalSize;
        var physicalSize = new PixelSize(
            Math.Max(1, (int)MathF.Round(logicalSize.Width * displayScale)),
            Math.Max(1, (int)MathF.Round(logicalSize.Height * displayScale)));
        sizeChanged = Size != physicalSize;
        Size = physicalSize;
        return scaleChanged || sizeChanged;
    }

    public void OnDecorationConfigured(PixelSize logicalSize) {
        if (_disposed) {
            return;
        }

        var firstConfigure = !IsConfigured;
        IsConfigured = true;
        var logicalSizeChanged = LogicalSize != logicalSize;
        _ = UpdateConfiguration(
            logicalSize,
            DisplayScale,
            out var scaleChanged,
            out var sizeChanged);
        if (logicalSizeChanged && _hasSurfaceContents) {
            // Keep the last committed buffer covering the new window area
            // while the UI renders the next physical-size frame. Without this
            // destination-only update, libdecor's synchronous parent commit can
            // expose the compositor's empty backdrop between configure events.
            ConfigureViewportDestination();
        }

        var requiresPresent = scaleChanged || sizeChanged;
        if (logicalSizeChanged && !requiresPresent && _hasSurfaceContents) {
            // The physical buffer and scale are unchanged, so the current
            // buffer can satisfy this configure after updating the viewport
            // destination. The caller commits that state after libdecor has
            // acknowledged the configure and applied the new window geometry.
            ConfigureBuffer(Size.Width, Size.Height);
            requiresPresent = false;
        } else if (firstConfigure || !_hasSurfaceContents) {
            // The initial configure and any recovery path must be acknowledged
            // by a real buffer commit. Committing an empty or stale surface
            // here can leave the window transparent or black until the first
            // frame arrives.
            requiresPresent = true;
        }

        _onConfigured(this, scaleChanged, sizeChanged, requiresPresent);
    }

    public void OnDecorationCloseRequested() {
        if (!_disposed) {
            _onCloseRequested(this);
        }
    }

    public void CommitSurface() {
        if (_disposed) {
            return;
        }

        _surface.Commit();
    }

    public void NotifySurfaceCommitted() {
        _hasSurfaceContents = true;
    }

    public bool TryAcquireBuffer(
        int width,
        int height,
        int stride,
        out WaylandShmBufferPool.WaylandShmBuffer? buffer) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bufferPool.TryAcquire(width, height, stride, out buffer);
    }

    public bool ArmFrameCallback(Action continuation) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _frameCallback.TryArm(_surface, _queue, continuation);
    }

    public void CancelFrameCallback() {
        if (!_disposed) {
            _frameCallback.Cancel();
        }
    }

    public void ConfigureBuffer(int width, int height) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_viewport is not null) {
            if (_configuredBufferScale != 1) {
                _surface.SetBufferScale(1);
                _configuredBufferScale = 1;
            }

            if (_configuredSourceWidth != width ||
                _configuredSourceHeight != height) {
                _viewport.SetSource(
                    new WlFixed(0),
                    new WlFixed(0),
                    new WlFixed(width),
                    new WlFixed(height));
                _configuredSourceWidth = width;
                _configuredSourceHeight = height;
            }

            ConfigureViewportDestination();
            ConfigureOpaqueRegion();
        } else {
            var bufferScale = Math.Max(
                1,
                (int)MathF.Round(DisplayScale));
            if (_configuredBufferScale != bufferScale) {
                _surface.SetBufferScale(bufferScale);
                _configuredBufferScale = bufferScale;
            }

            ConfigureOpaqueRegion();
        }
    }

    private void ConfigureViewportDestination() {
        if (_viewport is null) {
            return;
        }

        var destinationWidth = Math.Max(1, LogicalSize.Width);
        var destinationHeight = Math.Max(1, LogicalSize.Height);
        if (_configuredDestinationWidth != destinationWidth ||
            _configuredDestinationHeight != destinationHeight) {
            _viewport.SetDestination(
                destinationWidth,
                destinationHeight);
            _configuredDestinationWidth = destinationWidth;
            _configuredDestinationHeight = destinationHeight;
            ConfigureOpaqueRegion();
        }
    }

    private void ConfigureOpaqueRegion() {
        var width = Math.Max(1, LogicalSize.Width);
        var height = Math.Max(1, LogicalSize.Height);
        var size = new PixelSize(width, height);
        if (_opaqueRegionSize == size) {
            return;
        }

        // A top-level window is opaque unless the application explicitly
        // opts into a translucent surface. Advertising the whole content area
        // lets the compositor skip wallpaper blending and also prevents
        // transient alpha values in GPU swap-chain buffers from leaking
        // through while the surface is being resized.
        var region = _compositor.CreateRegion(null, null);
        try {
            region.Add(0, 0, width, height);
            _surface.SetOpaqueRegion(region);
        } finally {
            if (!region.IsDisposed) {
                region.Destroy();
                region.Dispose();
            }
        }

        _opaqueRegionSize = size;
    }

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

    private void OnPreferredBufferScale(int factor) {
        if (_disposed || HasFractionalScaling || factor <= 0) {
            return;
        }

        SetDisplayScale(factor);
    }

    private void OnPreferredFractionalScale(uint scale) {
        if (_disposed || scale == 0) {
            return;
        }

        SetDisplayScale(scale / 120f);
    }

    private void SetDisplayScale(float scale) {
        scale = NormalizeScale(scale);
        if (Math.Abs(DisplayScale - scale) < 0.0001f) {
            return;
        }

        _ = UpdateConfiguration(
            LogicalSize,
            scale,
            out var scaleChanged,
            out var sizeChanged);
        if (scaleChanged || sizeChanged) {
            _onConfigured(
                this,
                scaleChanged,
                sizeChanged,
                scaleChanged || sizeChanged);
        }
    }

    private static float NormalizeScale(float scale) {
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }

    private sealed class SurfaceListener : WlSurface.Listener {
        private readonly WaylandWindow _window;

        public SurfaceListener(WaylandWindow window) {
            _window = window;
        }

        protected override void PreferredBufferScale(
            WlSurface eventSender,
            int factor) {
            _window.OnPreferredBufferScale(factor);
        }
    }

    private sealed class FractionalScaleListener : WpFractionalScaleV1.Listener {
        private readonly WaylandWindow _window;

        public FractionalScaleListener(WaylandWindow window) {
            _window = window;
        }

        protected override void PreferredScale(
            WpFractionalScaleV1 eventSender,
            uint scale) {
            _window.OnPreferredFractionalScale(scale);
        }
    }
}
