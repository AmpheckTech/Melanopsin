// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Melanopsin.UI.Platform.Wayland;

public sealed partial class WaylandPlatformHost {
    public void CloseWindow(WindowId windowId) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.Remove(windowId, out var window)) {
            return;
        }

        _windowsByHandle.Remove(window.Handle);
        _configureTimestamps.Remove(windowId);
        _pendingFrameTimestamps.Remove(windowId);
        _frameCallbackTimestamps.Remove(windowId);
        window.Dispose();
        UpdateDisplayScale();
    }

    private WaylandWindow GetOrCreateWindow(WindowDescriptor descriptor) {
        if (!_windows.TryGetValue(descriptor.Id, out var window)) {
            return CreateWindow(descriptor);
        }

        if (!string.Equals(window.Title, descriptor.Title, StringComparison.Ordinal)) {
            window.SetTitle(descriptor.Title);
        }

        return window;
    }

    private WaylandWindow CreateWindow(WindowDescriptor descriptor) {
        var scale = DisplayScale;
        var logicalSize = new PixelSize(
            ToLogicalDimension(descriptor.Size.Width, scale),
            ToLogicalDimension(descriptor.Size.Height, scale));
        var window = new WaylandWindow(
            descriptor,
            _connection,
            descriptor.Size,
            logicalSize,
            scale,
            HandleWindowConfigured,
            HandleWindowCloseRequested,
            RetryPendingFrames);
        _windows.Add(window.Id, window);
        _windowsByHandle.Add(window.Handle, window);

        try {
            var parent = descriptor.OwnerId is { } ownerId &&
                _windows.TryGetValue(ownerId, out var owner)
                    ? owner.Decoration
                    : null;
            var decoration = _decorationContext.Decorate(
                window,
                window.Surface,
                descriptor.Title,
                logicalSize,
                parent);
            window.AttachDecoration(decoration);
            foreach (var child in _windows.Values) {
                if (child.OwnerId == window.Id) {
                    child.Decoration?.SetParent(decoration);
                }
            }

            _connection.FlushChecked();
        } catch {
            _windows.Remove(window.Id);
            _windowsByHandle.Remove(window.Handle);
            window.Dispose();
            throw;
        }

        return window;
    }

    private void HandleWindowConfigured(
        WaylandWindow window,
        bool scaleChanged,
        bool sizeChanged,
        bool requiresPresent) {
        UpdateDisplayScale();
        DropStalePendingFrame(window.Id, window);
        if (TracePresentation) {
            _configureTimestamps[window.Id] = Stopwatch.GetTimestamp();
            Console.Error.WriteLine(
                $"[wayland] configure window={window.Size.Width}x{window.Size.Height} " +
                $"logical={window.LogicalSize.Width}x{window.LogicalSize.Height} " +
                $"scale={window.DisplayScale:0.###} " +
                $"sizeChanged={sizeChanged} scaleChanged={scaleChanged} " +
                $"requiresPresent={requiresPresent}");
        }

        if (sizeChanged) {
            _events.QueueResized(window);
        }

        if (scaleChanged) {
            _events.QueueDpiChanged(window);
        }

        if (requiresPresent) {
            // Resize and DPI events may coalesce into no-ops. A recovery
            // configure still needs to dirty the window for a real commit.
            _events.QueueRenderRequested(window);
        }

        if (!window.IsShown) {
            window.IsShown = true;
            _events.QueueShown(window);
        }
    }

    private void HandleWindowCloseRequested(WaylandWindow window) {
        _events.QueueCloseRequested(window);
    }

    private void UpdateDisplayScale() {
        var scale = 1f;
        foreach (var window in _windows.Values) {
            scale = Math.Max(scale, window.DisplayScale);
        }

        Volatile.Write(ref _displayScale, NormalizeScale(scale));
    }

    private static int ToLogicalDimension(int physical, float scale) {
        return Math.Max(1, (int)MathF.Round(physical / NormalizeScale(scale)));
    }
}
