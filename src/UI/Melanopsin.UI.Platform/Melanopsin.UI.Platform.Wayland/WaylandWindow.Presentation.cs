// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using NWayland;
using NWayland.Protocols.Viewporter;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed partial class WaylandWindow {
    private readonly WaylandFrameCallback _frameCallback = new();
    private readonly WaylandShmBufferPool _bufferPool;
    private WpViewport? _viewport;
    private PixelSize? _opaqueRegionSize;
    private int _configuredBufferScale;
    private int _configuredSourceWidth;
    private int _configuredSourceHeight;
    private int _configuredDestinationWidth;
    private int _configuredDestinationHeight;

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
}
