using System.Diagnostics;
using Melanopsin.UI.Input;

namespace Melanopsin.UI.Hosting;

public sealed partial class Application {
    private void HandleEvent(WindowEvent windowEvent) {
        var window = _windows.FindWindow(windowEvent.WindowId);
        if (window is null) {
            return;
        }

        switch (windowEvent.Kind) {
            case WindowEventKind.CloseRequested:
                CloseWindow(window);
                break;
            case WindowEventKind.Resized:
                _lastResizeTimestamp = Stopwatch.GetTimestamp();
                window.Resize(windowEvent.Size);
                window.RaiseLifecycle(WindowLifecycleKind.Resized);
                break;
            case WindowEventKind.DpiChanged when windowEvent.DisplayScale > 0:
                _lastResizeTimestamp = Stopwatch.GetTimestamp();
                window.SetDisplayScale(windowEvent.DisplayScale);
                window.RaiseLifecycle(WindowLifecycleKind.DpiChanged);
                break;
            case WindowEventKind.PointerInput when windowEvent.Pointer is not null:
                var handled = _input.Process(
                    windowEvent.Pointer,
                    out var preventDefaultFocus);
                _gestures.Process(windowEvent.Pointer);
                var focusChanged = windowEvent.Pointer.Phase == PointerPhase.Down &&
                    !preventDefaultFocus &&
                    _focus.HandlePointer(windowEvent.Pointer);
                if (handled || focusChanged) {
                    window.InvalidatePaint();
                }

                break;
            case WindowEventKind.KeyInput when windowEvent.Key is not null:
                if (windowEvent.Key.Phase == KeyPhase.Down) {
                    PreviewKeyDown?.Invoke(windowEvent.Key);
                }

                if (_focus.HandleKey(windowEvent.Key)) {
                    window.InvalidatePaint();
                } else if (windowEvent.Key.Key == Key.Escape) {
                    CloseWindow(window);
                } else if (windowEvent.Key.Phase == KeyPhase.Down) {
                    UnhandledKeyDown?.Invoke(windowEvent.Key);
                }

                break;
            case WindowEventKind.TextComposition when
                windowEvent.Composition is not null:
                if (_focus.GetFocused(windowEvent.WindowId) is
                    ICompositionInputHandler compositionHandler &&
                    compositionHandler.HandleComposition(windowEvent.Composition)) {
                    window.InvalidatePaint();
                }

                break;
            case WindowEventKind.Shown:
                window.RaiseLifecycle(WindowLifecycleKind.Shown);
                break;
            case WindowEventKind.RenderRequested:
                window.InvalidatePaint();
                break;
            case WindowEventKind.Activated:
                window.RaiseLifecycle(WindowLifecycleKind.Activated);
                break;
            case WindowEventKind.Deactivated:
                window.RaiseLifecycle(WindowLifecycleKind.Deactivated);
                break;
            case WindowEventKind.Moved:
                window.RaiseLifecycle(
                    WindowLifecycleKind.Moved,
                    windowEvent.X,
                    windowEvent.Y);
                break;
        }
    }

    private void DrainPlatformEvents() {
        while (_host.TryDequeueEvent(out var windowEvent)) {
            switch (windowEvent.Kind) {
                case WindowEventKind.Resized:
                    RecordResize(windowEvent.WindowId, windowEvent.Size);
                    break;
                case WindowEventKind.DpiChanged when windowEvent.DisplayScale > 0:
                    RecordDisplayScale(
                        windowEvent.WindowId,
                        windowEvent.DisplayScale);
                    break;
                default:
                    FlushPendingWindowChanges();
                    HandleEvent(windowEvent);
                    break;
            }
        }

        FlushPendingWindowChanges();
    }

    private void RecordResize(WindowId windowId, PixelSize size) {
        _pendingWindowChanges.TryGetValue(windowId, out var change);
        change.Size = size;
        _pendingWindowChanges[windowId] = change;
    }

    private void RecordDisplayScale(WindowId windowId, float displayScale) {
        _pendingWindowChanges.TryGetValue(windowId, out var change);
        change.DisplayScale = displayScale;
        _pendingWindowChanges[windowId] = change;
    }

    private void FlushPendingWindowChanges() {
        if (_pendingWindowChanges.Count == 0) {
            return;
        }

        foreach (var (windowId, change) in _pendingWindowChanges) {
            var window = _windows.FindWindow(windowId);
            if (window is null) {
                continue;
            }

            if (change.DisplayScale is { } displayScale) {
                window.SetDisplayScale(displayScale);
                window.RaiseLifecycle(WindowLifecycleKind.DpiChanged);
            }

            if (change.Size is { } size) {
                window.Resize(size);
                window.RaiseLifecycle(WindowLifecycleKind.Resized);
            }
        }

        _lastResizeTimestamp = Stopwatch.GetTimestamp();
        _pendingWindowChanges.Clear();
    }

    private void CloseWindow(Window window) {
        var windowId = window.Id;
        List<Exception>? failures = null;
        if (_cursorWindowId == windowId) {
            _cursorWindowId = null;
            _cursorKind = null;
        }

        try {
            window.RaiseLifecycle(WindowLifecycleKind.CloseRequested);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            _input.CancelWindow(windowId);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            _focus.CancelWindow(windowId);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            _gestures.CancelWindow(windowId);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            _host.CloseWindow(windowId);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            _windows.CloseWindow(windowId, closePlatformHost: false);
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        if (failures is not null) {
            throw new AggregateException(
                $"Window '{windowId}' did not close cleanly.",
                failures);
        }
    }

    private struct PendingWindowChange {
        public PixelSize? Size { get; set; }

        public float? DisplayScale { get; set; }
    }
}
