// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Hosting;

public sealed partial class WindowManager {
    public Window CreateWindow(WindowOptions options) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);

        if (options.OwnerId is { } ownerId) {
            if (!_windowsById.ContainsKey(ownerId) ||
                _closingWindows.Contains(ownerId)) {
                throw new ArgumentException(
                    "The owner window must exist and must not be closing.",
                    nameof(options));
            }
        } else if (options.IsModal) {
            throw new ArgumentException(
                "A modal window must have an owner.",
                nameof(options));
        }

        var windowId = new WindowId(++_nextWindowId);
        var initialDisplayScale = ResolveDisplayScale();
        var initialPixelSize = ToPhysicalSize(options.Size, initialDisplayScale);
        var windowDescriptor = new WindowDescriptor(
            windowId,
            options.Title,
            initialPixelSize,
            options.OwnerId,
            options.IsModal);
        IRenderTarget? target = null;
        Window? window = null;
        try {
            target = _backend.CreateTarget(new RenderTargetDescriptor(initialPixelSize));
            var windowStyles = new StyleEngine(
                _styleEngine.Sheet,
                _styleEngine.Metrics);
            var renderContext = new WindowRenderContext(
                windowStyles,
                _layoutEngineFactory(windowStyles),
                _sceneComposerFactory(windowStyles));
            window = new Window(
                windowDescriptor,
                target,
                ReplaceTarget,
                renderContext,
                initialDisplayScale);
            _windowsById.Add(windowId, window);
            _windows.Add(window);
            if (options.OwnerId is { } registeredOwnerId) {
                if (!_ownedWindows.TryGetValue(registeredOwnerId, out var owned)) {
                    owned = [];
                    _ownedWindows.Add(registeredOwnerId, owned);
                }

                owned.Add(windowId);
            }

            window.RaiseLifecycle(WindowLifecycleKind.Opened);
            EnsureWindowCreationCompleted(window);
            WindowCreated?.Invoke(window);
            EnsureWindowCreationCompleted(window);
            return window;
        } catch (Exception exception) {
            if (window is not null) {
                _windows.Remove(window);
                _windowsById.Remove(window.Id);
                RemoveOwnedWindow(window);
            }

            try {
                if (window is not null) {
                    window.Dispose();
                } else {
                    target?.Dispose();
                }
            } catch (Exception cleanupException) {
                exception = new AggregateException(
                    "The window creation failed and cleanup also failed.",
                    exception,
                    cleanupException);
            }

            throw exception;
        }
    }

    public bool CloseWindow(WindowId windowId) {
        return CloseWindow(windowId, closePlatformHost: true);
    }

    internal bool CloseWindow(WindowId windowId, bool closePlatformHost) {
        ThrowIfDisposed();
        if (FindWindow(windowId) is null) {
            return false;
        }

        if (_closingWindows.Contains(windowId)) {
            return true;
        }

        var closeOrder = GetCloseOrder(windowId);
        foreach (var id in closeOrder) {
            _closingWindows.Add(id);
        }

        List<Exception>? failures = null;
        try {
            foreach (var id in closeOrder) {
                if (FindWindow(id) is null) {
                    continue;
                }

                try {
                    CloseSingleWindow(id, closePlatformHost);
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }
        } finally {
            foreach (var id in closeOrder) {
                _closingWindows.Remove(id);
            }
        }

        if (failures is not null) {
            throw new AggregateException(
                $"Window '{windowId}' and its owned windows did not close cleanly.",
                failures);
        }

        return true;
    }

    private void CloseSingleWindow(WindowId windowId, bool closePlatformHost) {
        var window = FindWindow(windowId);
        if (window is null) {
            return;
        }

        // Detach the window before invoking callbacks so reentrant close requests
        // cannot observe or dispose the same window twice.
        _windows.Remove(window);
        _windowsById.Remove(windowId);
        RemoveOwnedWindow(window);
        List<Exception>? failures = null;
        try {
            WindowClosing?.Invoke(window);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        if (closePlatformHost) {
            try {
                _platformHost?.CloseWindow(windowId);
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            }
        }

        try {
            window.Dispose();
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            WindowClosed?.Invoke(window);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        if (failures is not null) {
            throw new AggregateException(
                $"Window '{windowId}' did not close cleanly.",
                failures);
        }
    }

    private List<WindowId> GetCloseOrder(WindowId rootId) {
        var order = new List<WindowId>();
        var visited = new HashSet<WindowId>();
        Visit(rootId);
        return order;

        void Visit(WindowId windowId) {
            if (!visited.Add(windowId)) {
                return;
            }

            if (_ownedWindows.TryGetValue(windowId, out var children)) {
                foreach (var childId in children) {
                    Visit(childId);
                }
            }

            order.Add(windowId);
        }
    }

    private void RemoveOwnedWindow(Window window) {
        if (window.Descriptor.OwnerId is not { } ownerId ||
            !_ownedWindows.TryGetValue(ownerId, out var owned)) {
            return;
        }

        owned.Remove(window.Id);
        if (owned.Count == 0) {
            _ownedWindows.Remove(ownerId);
        }
    }

    private float ResolveDisplayScale() {
        var platformScale = _platformHost?.DisplayScale;
        return platformScale is > 0 && float.IsFinite(platformScale.Value)
            ? platformScale.Value
            : _displayScale;
    }

    private static PixelSize ToPhysicalSize(
        PixelSize logicalSize,
        float displayScale) {
        return new PixelSize(
            Math.Max(1, (int)MathF.Round(logicalSize.Width * displayScale)),
            Math.Max(1, (int)MathF.Round(logicalSize.Height * displayScale)));
    }

    private void EnsureWindowCreationCompleted(Window window) {
        if (!_windowsById.TryGetValue(window.Id, out var registered) ||
            !ReferenceEquals(registered, window) ||
            window.IsDisposed) {
            throw new InvalidOperationException(
                "A window was closed by an event handler before creation completed.");
        }
    }
}
