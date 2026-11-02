namespace Melanopsin.UI.DevTools;

public sealed partial class DevToolsComponent {
    public string OverviewButtonClass => PanelButtonClass(OverviewPanel);

    public string LayoutButtonClass => PanelButtonClass(LayoutPanel);

    public string PaintButtonClass => PanelButtonClass(PaintPanel);

    public string CachesButtonClass => PanelButtonClass(CachesPanel);

    public string MemoryButtonClass => PanelButtonClass(MemoryPanel);

    public string AccessibilityButtonClass => PanelButtonClass(AccessibilityPanel);

    public bool ShowOverview => SelectedTab == OverviewPanel;

    public bool ShowLayout => SelectedTab == LayoutPanel;

    public bool ShowPaint => SelectedTab == PaintPanel;

    public bool ShowCaches => SelectedTab == CachesPanel;

    public bool ShowMemory => SelectedTab == MemoryPanel;

    public bool ShowAccessibility => SelectedTab == AccessibilityPanel;

    public string PanelTitle => SelectedTab switch {
        LayoutPanel => "Layout",
        PaintPanel => "Paint",
        CachesPanel => "Caches",
        MemoryPanel => "Memory",
        AccessibilityPanel => "Accessibility",
        _ => "Overview",
    };

    public string PanelDescription => SelectedTab switch {
        LayoutPanel => "Measure and arrangement reuse",
        PaintPanel => "Damage and paint boundaries",
        CachesPanel => "Retained-frame activity",
        MemoryPanel => "Managed heap and framework retention",
        AccessibilityPanel => "Target visual settings",
        _ => "Frame timing and invalidation",
    };

    public void ActivateOverviewPanel() => SelectPanel(OverviewPanel);

    public void ActivateLayoutPanel() => SelectPanel(LayoutPanel);

    public void ActivatePaintPanel() => SelectPanel(PaintPanel);

    public void ActivateCachesPanel() => SelectPanel(CachesPanel);

    public void ActivateMemoryPanel() {
        SampleMemory(force: true);
        SelectPanel(MemoryPanel);
    }

    public void ActivateAccessibilityPanel() => SelectPanel(AccessibilityPanel);

    private void SelectPanel(string panel) {
        _selectedPanel.Value = panel;
    }

    private string PanelButtonClass(string panel) =>
        string.Equals(SelectedTab, panel, StringComparison.Ordinal)
            ? "nav selected"
            : "nav";
}
