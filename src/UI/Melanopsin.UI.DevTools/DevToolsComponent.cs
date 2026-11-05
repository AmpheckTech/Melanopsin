using Melanopsin.Reactive;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed partial class DevToolsComponent : Component {
    private const double DefaultSidebarWidth = 196;
    private const double MinimumSidebarWidth = 148;
    private const double MaximumSidebarWidth = 272;
    private const double FrameBudgetMilliseconds = 1000d / 60;
    private const string OverviewPanel = "overview";
    private const string LayoutPanel = "layout";
    private const string PaintPanel = "paint";
    private const string CachesPanel = "caches";
    private const string MemoryPanel = "memory";
    private const string AccessibilityPanel = "accessibility";
    private readonly Dictionary<Button, Action> _tabHandlers = [];
    private readonly Signal<string> _selectedPanel = new(OverviewPanel);
    private readonly Signal<DevToolsSnapshot?> _snapshot = new(null);
    private readonly Signal<DateTime?> _lastSnapshotAt = new(null);
    private readonly Signal<bool> _debugBounds = new(false);
    private List<DevToolsWindowInfo> _windows = [];
    private WindowId? _selectedWindowId;
    private double _sidebarWidth = DefaultSidebarWidth;

    public event Action<WindowId>? WindowSelected;

    public event Action<bool>? DebugBoundsChanged;

    public IReadOnlyList<DevToolsWindowInfo> Windows => _windows;

    public double SidebarWidth => _sidebarWidth;

    public void SetWindows(
        IReadOnlyList<DevToolsWindowInfo> windows,
        WindowId? selectedWindowId) {
        _windows = [.. windows];
        _selectedWindowId = selectedWindowId;
        Invalidate();
    }

    public void SetSnapshot(DevToolsSnapshot snapshot) {
        _lastSnapshotAt.Value = DateTime.Now;
        _snapshot.Value = snapshot;
        SampleMemory();
    }

    public void ClearSnapshot() {
        _snapshot.Value = null;
        _lastSnapshotAt.Value = null;
    }

    public void SetDebugBounds(bool enabled) {
        _debugBounds.Value = enabled;
    }

    public void ToggleDebugBounds() {
        if (_selectedWindowId is null) {
            return;
        }

        var enabled = !DebugBounds;
        SetDebugBounds(enabled);
        DebugBoundsChanged?.Invoke(enabled);
    }

    /// <summary>
    /// Runs a blocking collection and refreshes the memory panel. This is the
    /// only place DevTools forces a collection; ordinary sampling never does.
    /// </summary>
    public void CollectGarbage() {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        SampleMemory(force: true);
    }

    private void ResizeSidebar(double delta) {
        if (!double.IsFinite(delta)) {
            return;
        }

        _sidebarWidth = Math.Clamp(
            _sidebarWidth + delta,
            MinimumSidebarWidth,
            MaximumSidebarWidth);
    }

    protected override void OnBuildCompleted(Node root) {
        foreach (var pair in _tabHandlers) {
            pair.Key.Clicked -= pair.Value;
        }

        _tabHandlers.Clear();
        if (root is not Element element) {
            return;
        }

        AttachMemoryProbe(element);
        AttachHeapChart(element);

        var windowButtons = element
            .FindAll<Button>()
            .Where(button => HasClass(button, "tab"))
            .ToArray();
        for (var index = 0;
             index < Math.Min(windowButtons.Length, _windows.Count);
             index++) {
            var windowId = _windows[index].Id;
            Action handler = () => WindowSelected?.Invoke(windowId);
            windowButtons[index].Clicked += handler;
            _tabHandlers[windowButtons[index]] = handler;
        }
    }

    private static bool HasClass(Button button, string expected) {
        if (!button.Attributes.TryGetValue("class", out var value) ||
            value is not string classes) {
            return false;
        }

        return classes
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(expected, StringComparer.Ordinal);
    }
}
