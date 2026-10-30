using Melanopsin.Graphics;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed class DevToolsController : IDisposable {
    private readonly WindowManager _windows;
    private readonly DevToolsComponent _component = new();
    private readonly Dictionary<WindowId, Window> _observed = [];
    private readonly Dictionary<WindowId, DevToolsSnapshot> _snapshots = [];
    private Window? _window;
    private WindowId? _selectedWindowId;
    private long _lastDiagnosticsUpdate;
    private bool _creating;
    private bool _disposed;
    private Exception? _cleanupFailure;

    public DevToolsController(Application application) {
        ArgumentNullException.ThrowIfNull(application);
        _windows = application.Windows;
        DevToolsStyles.Install(_windows.Styles);
        _windows.WindowCreated += OnWindowCreated;
        _windows.WindowClosed += OnWindowClosed;
        _component.WindowSelected += SelectWindow;
        RefreshWindows();
    }

    public bool IsOpen => _window is not null;

    public Window? Window => _window;

    public WindowId? SelectedWindowId => _selectedWindowId;

    public int WindowCount => _observed.Count + (_window is null ? 0 : 1);

    public int TabCount => _component.Windows.Count;

    /// <summary>
    /// The failure observed during the most recent disposal. Synchronous
    /// <see cref="Dispose"/> records this value instead of throwing.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public void Show() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_window is not null) {
            return;
        }

        _creating = true;
        try {
            _window = _windows.CreateWindow(new WindowOptions {
                Title = "Melanopsin DevTools",
                Size = new PixelSize(720, 520),
            });
            _window.Root = new ComponentHost(_component);
        } finally {
            _creating = false;
        }

        RefreshWindows();
        UpdateSelectedWindow();
    }

    public void Hide() {
        if (_window is null) {
            return;
        }

        var window = _window;
        _window = null;
        _windows.CloseWindow(window.Id);
    }

    public void Toggle() {
        if (IsOpen) {
            Hide();
        } else {
            Show();
        }
    }

    public void SelectWindow(WindowId windowId) {
        if (_window is null) {
            return;
        }

        _selectedWindowId = windowId;
        RefreshWindows();
        UpdateSelectedWindow();
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        try {
            Hide();
        } catch (Exception exception) {
            failure = exception;
        }

        _windows.WindowCreated -= OnWindowCreated;
        _windows.WindowClosed -= OnWindowClosed;
        _component.WindowSelected -= SelectWindow;
        foreach (var window in _observed.Values) {
            window.DiagnosticsChanged -= OnDiagnosticsChanged;
        }

        _observed.Clear();
        _snapshots.Clear();
        Volatile.Write(ref _cleanupFailure, failure);
    }

    private void OnWindowCreated(Window window) {
        if (_creating || ReferenceEquals(window, _window)) {
            return;
        }

        Observe(window);
        RefreshWindows();
    }

    private void OnWindowClosed(Window window) {
        window.DiagnosticsChanged -= OnDiagnosticsChanged;
        _observed.Remove(window.Id);
        _snapshots.Remove(window.Id);
        RefreshWindows();
    }

    private void Observe(Window window) {
        if (!_observed.TryAdd(window.Id, window)) {
            return;
        }

        window.DiagnosticsChanged += OnDiagnosticsChanged;
        if (window.LastDiagnostics is { } snapshot) {
            _snapshots[window.Id] = snapshot;
        }
    }

    private void OnDiagnosticsChanged(
        Window window,
        DevToolsSnapshot snapshot) {
        _snapshots[window.Id] = snapshot;
        if (_window is not null && window.Id == _selectedWindowId) {
            var now = Environment.TickCount64;
            if (now - _lastDiagnosticsUpdate < 100) {
                return;
            }

            _lastDiagnosticsUpdate = now;
            _component.SetSnapshot(snapshot);
        }
    }

    private void RefreshWindows() {
        foreach (var window in _windows.Windows) {
            if (!ReferenceEquals(window, _window)) {
                Observe(window);
            }
        }

        if (_selectedWindowId is null ||
            _selectedWindowId != _window?.Id &&
            !_observed.ContainsKey(_selectedWindowId.Value)) {
            _selectedWindowId = _observed.Keys.FirstOrDefault();
        }

        if (_window is null) {
            return;
        }

        var tabs = _observed.Values
            .Select(window => new DevToolsWindowInfo(
                window.Id,
                window.Title,
                window.Id == _selectedWindowId
                    ? "devtools-tab devtools-tab-selected"
                    : "devtools-tab"))
            .ToList();
        tabs.Add(new DevToolsWindowInfo(
            _window.Id,
            _window.Title,
            _window.Id == _selectedWindowId
                ? "devtools-tab devtools-tab-selected"
                : "devtools-tab"));
        _component.SetWindows(tabs, _selectedWindowId);
    }

    private void UpdateSelectedWindow() {
        if (_window is null) {
            return;
        }

        if (_selectedWindowId == _window.Id) {
            _component.ClearSnapshot();
            return;
        }

        if (_selectedWindowId is { } selected &&
            _snapshots.TryGetValue(selected, out var snapshot)) {
            _lastDiagnosticsUpdate = Environment.TickCount64;
            _component.SetSnapshot(snapshot);
        } else {
            _component.ClearSnapshot();
        }
    }
}
