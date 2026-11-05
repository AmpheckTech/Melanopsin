using Melanopsin.UI.App;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Hosting;

/// <summary>
/// Owns multiple independent UI windows. All windows share a backend choice,
/// while each window keeps its own root tree, render context and render target.
/// </summary>
public sealed partial class WindowManager : IWindowHitTester, IDisposable {
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
    private readonly Dictionary<WindowId, Window> _windowsById = [];
    private readonly Dictionary<WindowId, List<WindowId>> _ownedWindows = [];
    private readonly HashSet<WindowId> _closingWindows = [];
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

    /// <summary>
    /// Raised after a window has been detached from the manager. Closing an
    /// owner raises this event for its descendants first, then for the owner.
    /// </summary>
    public event Action<Window>? WindowClosing;

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
        return _windowsById.GetValueOrDefault(windowId);
    }

    public IReadOnlyList<Window> GetOwnedWindows(WindowId ownerId) {
        ThrowIfDisposed();
        if (!_ownedWindows.TryGetValue(ownerId, out var ids)) {
            return [];
        }

        var owned = new List<Window>(ids.Count);
        foreach (var id in ids) {
            if (_windowsById.TryGetValue(id, out var window)) {
                owned.Add(window);
            }
        }

        return owned;
    }

    /// <summary>
    /// Whether input for this window is currently blocked by a modal descendant.
    /// Platform hosts may also enforce this state natively.
    /// </summary>
    public bool IsInputBlocked(WindowId windowId) {
        ThrowIfDisposed();
        var pending = new Stack<WindowId>();
        pending.Push(windowId);
        while (pending.TryPop(out var id)) {
            if (!_ownedWindows.TryGetValue(id, out var owned)) {
                continue;
            }

            foreach (var childId in owned) {
                if (!_windowsById.TryGetValue(childId, out var child)) {
                    continue;
                }

                if (child.Descriptor.IsModal) {
                    return true;
                }

                pending.Push(childId);
            }
        }

        return false;
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
                if (_platformHost is not null) {
                    try {
                        _platformHost.CloseWindow(window.Id);
                    } catch (Exception exception) {
                        (failures ??= []).Add(exception);
                    }
                }

                try {
                    window.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }
        } finally {
            _windows.Clear();
            _windowsById.Clear();
            _ownedWindows.Clear();
            _closingWindows.Clear();
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

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
