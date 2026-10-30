using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed partial class DevToolsComponent : Component {
    private readonly Dictionary<Button, Action> _tabHandlers = [];
    private List<DevToolsWindowInfo> _windows = [];
    private DevToolsSnapshot? _snapshot;
    private WindowId? _selectedWindowId;
    private TextBlock? _selectedText;
    private TextBlock? _frameText;
    private TextBlock? _layoutText;
    private TextBlock? _composeText;
    private TextBlock? _renderText;
    private TextBlock? _damageText;
    private TextBlock? _paintCacheText;
    private TextBlock? _relayoutCacheText;
    private TextBlock? _visionText;

    public event Action<WindowId>? WindowSelected;

    public IReadOnlyList<DevToolsWindowInfo> Windows => _windows;

    public string SelectedTitle =>
        _windows.FirstOrDefault(window => window.Id == _selectedWindowId)?.Title ??
        "No window selected";

    public string FrameText => _snapshot is { } snapshot
        ? $"Frame          {snapshot.FrameNumber}   Layout v{snapshot.LayoutVersion}"
        : "Frame          waiting for diagnostics";

    public string LayoutText => _snapshot is { } snapshot
        ? $"Layout         {snapshot.LayoutMilliseconds:0.00} ms"
        : "Layout         —";

    public string ComposeText => _snapshot is { } snapshot
        ? $"Compose        {snapshot.ComposeMilliseconds:0.00} ms"
        : "Compose        —";

    public string RenderText => _snapshot is { } snapshot
        ? $"Raster         {snapshot.RenderMilliseconds:0.00} ms · {snapshot.CommandCount} commands"
        : "Raster         —";

    public string DamageText => _snapshot is { } snapshot
        ? $"Damage         {snapshot.DamageRects} rect · {snapshot.DamageArea:N0} px"
        : "Damage         —";

    public string PaintCacheText => _snapshot is { } snapshot
        ? $"Paint cache    {snapshot.ReusedPaintBoundaries} reuse · " +
          $"{snapshot.RepaintedPaintBoundaries} repaint · raster " +
          $"{snapshot.RasterCacheHits}/{snapshot.RasterCacheMisses} " +
          $"({snapshot.RasterCacheBytes / 1024d:0.0} KiB)"
        : "Paint cache    —";

    public string RelayoutCacheText => _snapshot is { } snapshot
        ? $"Layout cache   {snapshot.InvalidationScope} · " +
          $"path {snapshot.InvalidationPathNodeCount} · " +
          $"rebuilt {snapshot.ArrangeReuseMisses} · measure {snapshot.RelayoutBoundaryHits}/" +
          $"{snapshot.RelayoutBoundaryMisses} · exec {snapshot.MeasureExecutions} · " +
          $"reuse {snapshot.PersistentMeasureHits} · arrange " +
          $"{snapshot.ArrangeReuseHits}/{snapshot.ArrangeReuseMisses} · " +
          $"hit {snapshot.HitRegionCacheHits}/{snapshot.HitRegionCacheMisses}"
        : "Layout cache —";

    public string VisionText => _snapshot is { } snapshot
        ? $"Vision         {snapshot.VisualAccessibility.ColorVision} · " +
          $"contrast={snapshot.VisualAccessibility.HighContrast}"
        : "Vision         —";

    public void SetWindows(
        IReadOnlyList<DevToolsWindowInfo> windows,
        WindowId? selectedWindowId) {
        _windows = [.. windows];
        _selectedWindowId = selectedWindowId;
        Invalidate();
    }

    public void SetSnapshot(DevToolsSnapshot snapshot) {
        _snapshot = snapshot;
        ApplySnapshot();
    }

    public void ClearSnapshot() {
        _snapshot = null;
        ApplySnapshot();
    }

    protected override void OnBuildCompleted(Node root) {
        foreach (var pair in _tabHandlers) {
            pair.Key.Clicked -= pair.Value;
        }

        _tabHandlers.Clear();
        if (root is not Element element) {
            return;
        }

        _selectedText = element.FindById("devtools-selected") as TextBlock;
        _frameText = element.FindById("devtools-frame") as TextBlock;
        _layoutText = element.FindById("devtools-layout") as TextBlock;
        _composeText = element.FindById("devtools-compose") as TextBlock;
        _renderText = element.FindById("devtools-render") as TextBlock;
        _damageText = element.FindById("devtools-damage") as TextBlock;
        _paintCacheText = element.FindById("devtools-paint-cache") as TextBlock;
        _relayoutCacheText = element.FindById("devtools-relayout-cache") as TextBlock;
        _visionText = element.FindById("devtools-vision") as TextBlock;
        var buttons = element.FindAll<Button>().ToArray();
        for (var index = 0; index < Math.Min(buttons.Length, _windows.Count); index++) {
            var windowId = _windows[index].Id;
            Action handler = () => WindowSelected?.Invoke(windowId);
            buttons[index].Clicked += handler;
            _tabHandlers[buttons[index]] = handler;
        }

        ApplySnapshot();
    }

    private void ApplySnapshot() {
        if (_selectedText is not null) {
            _selectedText.Text = SelectedTitle;
            _frameText!.Text = FrameText;
            _layoutText!.Text = LayoutText;
            _composeText!.Text = ComposeText;
            _renderText!.Text = RenderText;
            _damageText!.Text = DamageText;
            _paintCacheText!.Text = PaintCacheText;
            _relayoutCacheText!.Text = RelayoutCacheText;
            _visionText!.Text = VisionText;
        }
    }
}
