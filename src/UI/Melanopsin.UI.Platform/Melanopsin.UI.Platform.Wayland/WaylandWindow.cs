// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Rendering;
using NWayland;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed partial class WaylandWindow : IDisposable {
    private readonly WlCompositor _compositor;
    private readonly WlSurface _surface;
    private readonly IWlTargetQueue _queue;
    private readonly SurfaceListener _surfaceListener;
    private readonly Action<WaylandWindow, bool, bool, bool> _onConfigured;
    private readonly Action<WaylandWindow> _onCloseRequested;
    private readonly Action _onBufferReleased;
    private bool _hasSurfaceContents;

    public WaylandWindow(
        WindowDescriptor descriptor,
        WaylandConnection connection,
        PixelSize initialPhysicalSize,
        PixelSize initialLogicalSize,
        float displayScale,
        Action<WaylandWindow, bool, bool, bool> onConfigured,
        Action<WaylandWindow> onCloseRequested,
        Action onBufferReleased) {
        Id = descriptor.Id;
        OwnerId = descriptor.OwnerId;
        IsModal = descriptor.IsModal;
        _queue = connection.Queue;
        _onConfigured = onConfigured;
        _onCloseRequested = onCloseRequested;
        _onBufferReleased = onBufferReleased;
        Title = descriptor.Title;
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

    public WindowId? OwnerId { get; }

    public bool IsModal { get; }

    public WaylandPendingFrame PendingFrame { get; } = new();

    public INativePresentationSurface? NativePresentationSurface { get; set; }

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

    private static float NormalizeScale(float scale) {
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }
}
