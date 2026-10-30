using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.App;
using Melanopsin.Reactive;
using Melanopsin.UI.Input;
using Melanopsin.UI.Threading;

namespace Melanopsin.UI.Hosting;

/// <summary>
/// Minimal single-threaded frame and platform-event loop. Input routing will
/// remain above this layer.
/// </summary>
public sealed class Application : IDisposable, IAsyncDisposable {
    private static readonly long ResizeActivityWindowTicks =
        (long)(Stopwatch.Frequency * 0.2);

    private readonly WindowManager _windows;
    private readonly IPlatformWindowHost _host;
    private RenderOptions _renderOptions;
    private readonly InputManager _input;
    private readonly FocusManager _focus;
    private readonly AccessibilityManager _accessibility;
    private readonly GestureManager _gestures;
    private readonly UiDispatcher _dispatcher;
    private readonly UiRuntime _runtime;
    private readonly Dictionary<WindowId, PendingWindowChange> _pendingWindowChanges = [];
    private WindowId? _cursorWindowId;
    private CursorKind? _cursorKind;
    private IDisposable? _dispatcherScope;
    private IDisposable? _reactiveSchedulerScope;
    private long _lastAccessibilityPublish;
    private long _lastResizeTimestamp;
    private int _isDisposed;
    private Exception? _cleanupFailure;

    public Application(
        WindowManager windows,
        IPlatformWindowHost host,
        RenderOptions? renderOptions = null,
        InputManager? input = null,
        FocusManager? focus = null,
        AccessibilityManager? accessibility = null,
        GestureManager? gestures = null,
        UiDispatcher? dispatcher = null,
        UiRuntime? runtime = null) {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(host);
        _windows = windows;
        _host = host;
        _renderOptions = renderOptions ?? RenderOptions.Default;
        _input = input ?? new InputManager(windows);
        _focus = focus ?? new FocusManager(windows);
        _accessibility = accessibility ?? new AccessibilityManager(windows);
        _gestures = gestures ?? new GestureManager(windows);
        _runtime = runtime ??
            new UiRuntime(dispatcher ?? new UiDispatcher());
        _dispatcher = _runtime.Dispatcher;
        _windows.WindowCreated += OnWindowCreated;
        foreach (var window in _windows.Windows) {
            window.AttachRuntime(_runtime);
        }

        _dispatcherScope = _dispatcher.InstallAsDefault();
        _reactiveSchedulerScope = ReactiveScheduler.Push(_dispatcher);
    }

    public event Action<KeyInputEvent>? UnhandledKeyDown;

    public UiDispatcher Dispatcher => _dispatcher;

    public UiRuntime Runtime => _runtime;

    public WindowManager Windows => _windows;

    public ColorVisionMode ColorVision =>
        _renderOptions.VisualAccessibility.ColorVision;

    public bool HighContrast =>
        _renderOptions.VisualAccessibility.HighContrast;

    public RenderOptions RenderOptions => _renderOptions;

    /// <summary>
    /// The failure observed during the most recent synchronous or asynchronous
    /// disposal. <see cref="Dispose"/> records this value instead of throwing.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public void SetColorVisionMode(ColorVisionMode mode) {
        _renderOptions = _renderOptions with {
            VisualAccessibility =
                _renderOptions.VisualAccessibility with {
                    ColorVision = mode,
                },
        };
        InvalidateAllWindows();
    }

    public void SetHighContrast(bool enabled) {
        _renderOptions = _renderOptions with {
            VisualAccessibility =
                _renderOptions.VisualAccessibility with {
                    HighContrast = enabled,
                },
        };
        InvalidateAllWindows();
    }

    public int ActiveFrameRate { get; set; } = 60;

    public int IdleFrameRate { get; set; } = 15;

    public void Run(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _isDisposed) != 0,
            this);
        if (_dispatcher.IsStopped) {
            return;
        }

        _dispatcherScope ??= _dispatcher.InstallAsDefault();
        _reactiveSchedulerScope ??= ReactiveScheduler.Push(_dispatcher);
        var lastTick = Stopwatch.GetTimestamp();
        try {
            _runtime.Start();
            while (!_dispatcher.IsStopped &&
                   _windows.Windows.Count != 0) {
                cancellationToken.ThrowIfCancellationRequested();
                var frameStart = Stopwatch.GetTimestamp();
                var elapsedSeconds = Math.Clamp(
                    (float)((frameStart - lastTick) / (double)Stopwatch.Frequency),
                    0,
                    0.1f);
                lastTick = frameStart;
                _dispatcher.Drain();
                DrainPlatformEvents();

                if (_windows.Windows.Count == 0) {
                    break;
                }

                foreach (var window in _windows.Windows.ToArray()) {
                    _ = _focus.Reconcile(window.Id);
                }

                var animating = _windows.AdvanceAnimations(elapsedSeconds);
                var frames = _windows.RenderDirty(_renderOptions);
                RefreshCursor();
                var now = Environment.TickCount64;
                if (frames.Count != 0 && now - _lastAccessibilityPublish >= 100) {
                    _accessibility.PublishAll();
                    _lastAccessibilityPublish = now;
                }

                _gestures.Advance();
                if (!animating &&
                    frames.Count == 0 &&
                    _input.ActivePointerCount == 0 &&
                    !_dispatcher.HasPendingWork) {
                    _host.WaitForEvent(timeoutMilliseconds: 100);
                    continue;
                }

                var resizeActive = _lastResizeTimestamp != 0 &&
                    frameStart - _lastResizeTimestamp <= ResizeActivityWindowTicks;
                var targetFrameRate =
                    animating || _input.ActivePointerCount > 0 || resizeActive
                    ? Math.Clamp(ActiveFrameRate, 30, 240)
                    : Math.Clamp(IdleFrameRate, 15, 240);
                var intervalTicks = Stopwatch.Frequency / (double)targetFrameRate;
                var remainingTicks = intervalTicks - (Stopwatch.GetTimestamp() - frameStart);
                if (resizeActive &&
                    frames.Count == 0 &&
                    !_dispatcher.HasPendingWork) {
                    // No frame was produced because presentation is
                    // backpressured. Let the compositor's configure and
                    // frame-callback stream pace the resize instead of
                    // spinning and competing with it.
                    _host.WaitForEvent(timeoutMilliseconds: 100);
                } else if (frames.Count != 0 && !_dispatcher.HasPendingWork) {
                    if (remainingTicks > 0) {
                        // Let the platform event source pace frames when
                        // possible. On Wayland this wakes on the compositor
                        // frame callback; then enforce the requested frame
                        // interval as a hard cap for normal rendering.
                        _host.WaitForEvent(timeoutMilliseconds: Math.Clamp(
                            (int)Math.Ceiling(
                                (remainingTicks / Stopwatch.Frequency) * 1000),
                            1,
                            int.MaxValue));

                        remainingTicks = intervalTicks -
                            (Stopwatch.GetTimestamp() - frameStart);
                        if (remainingTicks > 0) {
                            Thread.Sleep(Math.Max(
                                1,
                                (int)Math.Floor(
                                    (remainingTicks / Stopwatch.Frequency) * 1000)));
                        }
                    }
                } else if (!resizeActive && remainingTicks > 0) {
                    var sleepMilliseconds = Math.Max(
                        1,
                        (int)Math.Floor((remainingTicks / Stopwatch.Frequency) * 1000));
                    Thread.Sleep(sleepMilliseconds);
                }
            }
        } finally {
            try {
                // Even when Dispose was requested from another thread, the
                // owner loop must be the one to drain and finalize the queue.
                _runtime.Stop();
            } finally {
                RestoreDispatcherDefault();
            }
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) {
            return;
        }

        _windows.WindowCreated -= OnWindowCreated;
        Exception? failure = null;
        try {
            _runtime.Dispose();
            failure = _runtime.LastStopFailure;
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                RestoreDispatcherDefault();
            } catch (Exception exception) {
                failure = CombineFailures(failure, exception);
            }
        }

        Volatile.Write(ref _cleanupFailure, failure);
    }

    public async ValueTask DisposeAsync() {
        _ = Interlocked.Exchange(ref _isDisposed, 1);
        _windows.WindowCreated -= OnWindowCreated;
        Exception? failure = null;
        try {
            await _runtime.DisposeAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                RestoreDispatcherDefault();
            } catch (Exception exception) {
                failure = CombineFailures(failure, exception);
            }
        }

        Volatile.Write(ref _cleanupFailure, failure);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Exception CombineFailures(
        Exception? first,
        Exception second) {
        return first is null
            ? second
            : new AggregateException(first, second);
    }

    private void OnWindowCreated(Window window) {
        window.AttachRuntime(_runtime);
    }

    private void RestoreDispatcherDefault() {
        _reactiveSchedulerScope?.Dispose();
        _reactiveSchedulerScope = null;
        _dispatcherScope?.Dispose();
        _dispatcherScope = null;
    }

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
                var focusChanged = false;
                if (windowEvent.Pointer.Phase == PointerPhase.Down) {
                    focusChanged = _focus.HandlePointer(windowEvent.Pointer);
                }

                var handled = _input.Process(windowEvent.Pointer);
                _gestures.Process(windowEvent.Pointer);
                if (handled || focusChanged) {
                    window.InvalidatePaint();
                }

                break;
            case WindowEventKind.KeyInput when windowEvent.Key is not null:
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

    private struct PendingWindowChange {
        public PixelSize? Size { get; set; }

        public float? DisplayScale { get; set; }
    }

    private void CloseWindow(Window window) {
        ArgumentNullException.ThrowIfNull(window);
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

    private void InvalidateAllWindows() {
        foreach (var window in _windows.Windows) {
            window.InvalidatePaint();
        }
    }

    private void RefreshCursor() {
        foreach (var window in _windows.Windows) {
            if (!_input.TryGetMouseTarget(window.Id, out var target) ||
                target is null) {
                continue;
            }

            var cursor = window.RenderContext.Styles.ResolveCursor(target);
            if (_cursorWindowId != window.Id || _cursorKind != cursor) {
                _host.SetCursor(window.Id, cursor);
                _cursorWindowId = window.Id;
                _cursorKind = cursor;
            }

            return;
        }

        if (_cursorWindowId is { } previousWindowId &&
            _windows.FindWindow(previousWindowId) is not null) {
            _host.SetCursor(previousWindowId, CursorKind.Auto);
        }

        _cursorWindowId = null;
        _cursorKind = null;
    }
}
