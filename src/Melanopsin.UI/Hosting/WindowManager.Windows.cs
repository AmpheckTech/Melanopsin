using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Hosting;

public sealed partial class WindowManager {
    public Window CreateWindow(WindowOptions options) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);

        var windowId = new WindowId(++_nextWindowId);
        var initialDisplayScale = ResolveDisplayScale();
        var initialPixelSize = ToPhysicalSize(options.Size, initialDisplayScale);
        var windowDescriptor = new WindowDescriptor(
            windowId,
            options.Title,
            initialPixelSize);
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
            window.RaiseLifecycle(WindowLifecycleKind.Opened);
            EnsureWindowCreationCompleted(window);
            WindowCreated?.Invoke(window);
            EnsureWindowCreationCompleted(window);
            return window;
        } catch (Exception exception) {
            if (window is not null) {
                _windows.Remove(window);
                _windowsById.Remove(window.Id);
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
        var window = FindWindow(windowId);
        if (window is null) {
            return false;
        }

        // Remove the window first so callbacks and reentrant close requests cannot
        // observe or dispose the same window twice.
        _windows.Remove(window);
        _windowsById.Remove(windowId);
        List<Exception>? failures = null;
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

        return true;
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
