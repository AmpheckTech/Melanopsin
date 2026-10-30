using System.Diagnostics;
using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Hosting;

/// <summary>
/// Owns multiple independent UI windows. All windows share a backend choice,
/// while each window keeps its own root tree, render context and render target.
/// </summary>
public sealed class WindowManager : IWindowHitTester, IDisposable {
    private static readonly bool TraceFrameTiming = string.Equals(
        Environment.GetEnvironmentVariable("MELANOPSIN_FRAME_TRACE"),
        "1",
        StringComparison.Ordinal);

    private readonly IRenderBackend _backend;
    private readonly IFramePresenter? _presenter;
    private readonly IPlatformWindowHost? _platformHost;
    private readonly StyleEngine _styleEngine;
    private readonly Func<StyleEngine, ILayoutEngine> _layoutEngineFactory;
    private readonly Func<StyleEngine, IRenderSceneComposer> _sceneComposerFactory;
    private readonly float _displayScale;
    private readonly bool _ownsBackend;
    private readonly List<Window> _windows = [];
    private readonly IReadOnlyList<Window> _windowsView;
    private long _nextWindowId;
    private int _disposed;
    private Exception? _cleanupFailure;

    public WindowManager(
        IRenderBackend backend,
        IFramePresenter? presenter = null,
        StyleEngine? styleEngine = null,
        bool ownsBackend = true,
        IPlatformWindowHost? platformHost = null,
        float displayScale = 1f,
        Func<StyleEngine, ILayoutEngine>? layoutEngineFactory = null,
        Func<StyleEngine, IRenderSceneComposer>? sceneComposerFactory = null) {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayScale);
        _backend = backend;
        _presenter = presenter;
        _platformHost = platformHost ?? presenter as IPlatformWindowHost;
        var resolvedStyleEngine = styleEngine ?? new StyleEngine();
        _styleEngine = resolvedStyleEngine;
        _layoutEngineFactory = layoutEngineFactory ??
            (styles => new FlowLayoutEngine(styles));
        _sceneComposerFactory = sceneComposerFactory ??
            (styles => new DefaultRenderSceneComposer(styles));
        _displayScale = displayScale;
        _ownsBackend = ownsBackend;
        _windowsView = _windows.AsReadOnly();
    }

    public IRenderBackend Backend => _backend;

    public IReadOnlyList<Window> Windows => _windowsView;

    public StyleEngine Styles => _styleEngine;

    public event Action<Window>? WindowCreated;

    public event Action<Window>? WindowClosed;

    public long TotalFramesPresented { get; private set; }

    /// <summary>
    /// The failure observed during the most recent disposal. Synchronous
    /// <see cref="Dispose"/> records this value instead of throwing.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public int ActiveStyleTransitionCount =>
        _windows.Sum(static window =>
            window.RenderContext.ActiveTransitionCount);

    public Window? FindWindow(WindowId windowId) {
        ThrowIfDisposed();
        return _windows.Find(candidate => candidate.Id == windowId);
    }

    IEnumerable<WindowId> IWindowTreeQuery.WindowIds =>
        _windows.Select(static window => window.Id);

    bool IWindowTreeQuery.ContainsWindow(WindowId windowId) {
        return FindWindow(windowId) is not null;
    }

    Node? IWindowTreeQuery.GetRoot(WindowId windowId) {
        return FindWindow(windowId)?.Layout?.Root.Node;
    }

    bool IWindowTreeQuery.TryGetBounds(
        WindowId windowId,
        Node node,
        out Rect bounds) {
        ArgumentNullException.ThrowIfNull(node);
        if (FindWindow(windowId)?.Layout is { } layout &&
            layout.TryGetNode(node, out var layoutNode)) {
            bounds = layoutNode.Bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    WindowHitTarget? IWindowHitTester.HitTest(
        WindowId windowId,
        float x,
        float y) {
        var hit = FindWindow(windowId)?.HitTest(x, y);
        return hit is null
            ? null
            : new WindowHitTarget(hit.Node, hit.Bounds, hit.Route);
    }

    public Window CreateWindow(WindowOptions options) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);

        var windowId = new WindowId(++_nextWindowId);
        var windowDescriptor = new WindowDescriptor(
            windowId,
            options.Title,
            options.Size);
        var renderTargetDescriptor = new RenderTargetDescriptor(options.Size);
        IRenderTarget? target = null;
        Window? window = null;
        try {
            target = _backend.CreateTarget(renderTargetDescriptor);
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
                _displayScale);
            _windows.Add(window);
            window.RaiseLifecycle(WindowLifecycleKind.Opened);
            EnsureWindowCreationCompleted(window);
            WindowCreated?.Invoke(window);
            EnsureWindowCreationCompleted(window);
            return window;
        } catch (Exception exception) {
            if (window is not null) {
                _windows.Remove(window);
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

    private void EnsureWindowCreationCompleted(Window window) {
        if (!_windows.Contains(window) || window.IsDisposed) {
            throw new InvalidOperationException(
                "A window was closed by an event handler before creation completed.");
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

    public IReadOnlyList<RenderFrame> RenderAll(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: false);
    }

    public IReadOnlyList<RenderFrame> RenderDirty(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: true);
    }

    public bool AdvanceAnimations(float elapsedSeconds) {
        ThrowIfDisposed();
        var changed = false;
        foreach (var window in _windows) {
            changed |= window.AdvanceAnimations(elapsedSeconds);
        }

        return changed;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        List<Exception>? failures = null;
        try {
            foreach (var window in _windows) {
                try {
                    window.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }
        } finally {
            _windows.Clear();
            if (_ownsBackend) {
                try {
                    _backend.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }
        }

        Exception? failure = failures is null
            ? null
            : new AggregateException(
                "One or more windows failed to dispose cleanly.",
                failures);
        Volatile.Write(ref _cleanupFailure, failure);
    }

    private IReadOnlyList<RenderFrame> RenderWindows(
        RenderOptions options,
        bool dirtyOnly) {
        var frames = new List<RenderFrame>(_windows.Count);
        var layoutChanged = false;

        foreach (var window in _windows.ToArray()) {
            if (dirtyOnly && !window.IsDirty) {
                continue;
            }

            // Do not draw into an offscreen target while the platform is
            // waiting for the compositor's frame callback. This mirrors
            // Avalonia's RenderTargetNotReady/State.IsReady behavior and keeps
            // interactive resize frames coalesced instead of rendering every
            // configure event.
            if (_platformHost is not null &&
                !_platformHost.IsPresentationReady(window.Descriptor)) {
                continue;
            }

            var layoutRevision = window.LayoutVersion;
            var renderStarted = Stopwatch.GetTimestamp();
            var frame = window.Render(options);

            var presentStarted = Stopwatch.GetTimestamp();
            _presenter?.Present(window.Descriptor, frame);
            if (TraceFrameTiming) {
                Console.Error.WriteLine(
                    $"[ui] frame {window.Id} {frame.Descriptor.Size.Width}x{frame.Descriptor.Size.Height} " +
                    $"layout={window.LastDiagnostics?.LayoutMilliseconds ?? 0:0.00}ms " +
                    $"compose={window.LastDiagnostics?.ComposeMilliseconds ?? 0:0.00}ms " +
                    $"raster={window.LastDiagnostics?.RenderMilliseconds ?? 0:0.00}ms " +
                    $"render-total={Stopwatch.GetElapsedTime(renderStarted, presentStarted).TotalMilliseconds:0.00}ms " +
                    $"present-call={Stopwatch.GetElapsedTime(presentStarted).TotalMilliseconds:0.00}ms");
            }

            TotalFramesPresented++;
            window.MarkClean();
            layoutChanged |= window.LayoutVersion != layoutRevision;
            frames.Add(frame);
        }

        if (layoutChanged) {
            PruneStyleState();
        }

        return frames;
    }

    private IRenderTarget ReplaceTarget(RenderTargetDescriptor descriptor) {
        ThrowIfDisposed();
        return _backend.CreateTarget(descriptor);
    }

    private void PruneStyleState() {
        var liveNodes = new HashSet<Node>(
            ReferenceEqualityComparer.Instance);
        foreach (var window in _windows) {
            if (window.Layout is not { } layout) {
                continue;
            }

            foreach (var node in layout.Nodes) {
                liveNodes.Add(node);
            }
        }

        foreach (var window in _windows) {
            window.RenderContext.Prune(liveNodes);
        }
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
