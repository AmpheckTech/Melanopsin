// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed partial class DevToolsComponent {
    public bool HasSnapshot => _snapshot.Value is not null;

    public string SelectedTab => _selectedPanel.Value;

    public string SelectedTitle =>
        _windows.FirstOrDefault(window => window.Id == _selectedWindowId)?.Title ??
        "No target";

    public string SelectedContext => _selectedWindowId is { } id
        ? $"Window {id.Value}"
        : "No application window";

    public string SnapshotState => HasSnapshot ? "LIVE" : "WAITING";

    public string SnapshotStateLabel => $"● {SnapshotState}";

    public bool DebugBounds => _debugBounds.Value;

    public bool DebugBoundsUnavailable => _selectedWindowId is null;

    public string DebugBoundsValue => DebugBounds ? "Enabled" : "Disabled";

    public string DebugBoundsLabel => _selectedWindowId is null
        ? "Layout boxes"
        : DebugBounds ? "Hide boxes" : "Show boxes";

    public string DebugBoundsButtonClass => DebugBounds
        ? "overlay selected"
        : "overlay";

    public string SnapshotStateClass => HasSnapshot
        ? "caption live"
        : "caption waiting";

    public string SnapshotStateDotClass => HasSnapshot
        ? "dot live"
        : "dot waiting";

    public string WindowCountValue => _windows.Count == 1
        ? "1 window"
        : $"{_windows.Count} windows";

    public string LastUpdatedValue => _lastSnapshotAt.Value?.ToString("HH:mm:ss") ?? "—";

    public string StatusSummaryValue =>
        $"{WindowCountValue} · 10 Hz · Updated {LastUpdatedValue}";

    public string FrameValue => _snapshot.Value?.FrameNumber.ToString("N0") ?? "—";

    public string LayoutVersionValue => _snapshot.Value is { } snapshot
        ? $"v{snapshot.LayoutVersion}"
        : "—";

    public string LayoutMillisecondsValue =>
        FormatMilliseconds(_snapshot.Value?.LayoutMilliseconds);

    public string ComposeMillisecondsValue =>
        FormatMilliseconds(_snapshot.Value?.ComposeMilliseconds);

    public string RenderMillisecondsValue =>
        FormatMilliseconds(_snapshot.Value?.RenderMilliseconds);

    public string CommandCountValue =>
        _snapshot.Value?.CommandCount.ToString("N0") ?? "—";

    public string DamageRectCountValue =>
        _snapshot.Value?.DamageRects.ToString("N0") ?? "—";

    public string DamageAreaValue => _snapshot.Value is { } snapshot
        ? $"{snapshot.DamageArea:N0} px²"
        : "—";

    public string ReusedPaintBoundariesValue =>
        _snapshot.Value?.ReusedPaintBoundaries.ToString("N0") ?? "—";

    public string RepaintedPaintBoundariesValue =>
        _snapshot.Value?.RepaintedPaintBoundaries.ToString("N0") ?? "—";

    public string RasterCacheHitsValue =>
        _snapshot.Value?.RasterCacheHits.ToString("N0") ?? "—";

    public string RasterCacheMissesValue =>
        _snapshot.Value?.RasterCacheMisses.ToString("N0") ?? "—";

    public string RasterCacheSizeValue => _snapshot.Value is { } snapshot
        ? $"{snapshot.RasterCacheBytes / 1024d:0.0} KiB"
        : "—";

    public string RasterCacheHitRateValue => _snapshot.Value is { } snapshot
        ? FormatHitRate(snapshot.RasterCacheHits, snapshot.RasterCacheMisses)
        : "—";

    public string InvalidationScopeValue => _snapshot.Value?.InvalidationScope switch {
        LayoutInvalidationScope.MergedPaths => "Merged paths",
        LayoutInvalidationScope.Full => "Full tree",
        LayoutInvalidationScope.None => "None",
        _ => "—",
    };

    public string InvalidationPathNodeCountValue =>
        _snapshot.Value?.InvalidationPathNodeCount.ToString("N0") ?? "—";

    public string RelayoutBoundaryHitsValue =>
        _snapshot.Value?.RelayoutBoundaryHits.ToString("N0") ?? "—";

    public string RelayoutBoundaryMissesValue =>
        _snapshot.Value?.RelayoutBoundaryMisses.ToString("N0") ?? "—";

    public string ArrangeReuseHitsValue =>
        _snapshot.Value?.ArrangeReuseHits.ToString("N0") ?? "—";

    public string ArrangeReuseMissesValue =>
        _snapshot.Value?.ArrangeReuseMisses.ToString("N0") ?? "—";

    public string HitRegionCacheHitsValue =>
        _snapshot.Value?.HitRegionCacheHits.ToString("N0") ?? "—";

    public string HitRegionCacheMissesValue =>
        _snapshot.Value?.HitRegionCacheMisses.ToString("N0") ?? "—";

    public string HitRegionCacheHitRateValue => _snapshot.Value is { } snapshot
        ? FormatHitRate(snapshot.HitRegionCacheHits, snapshot.HitRegionCacheMisses)
        : "—";

    public string MeasureExecutionValue =>
        _snapshot.Value?.MeasureExecutions.ToString("N0") ?? "—";

    public string PersistentMeasureHitsValue =>
        _snapshot.Value?.PersistentMeasureHits.ToString("N0") ?? "—";

    public string LayoutNodeCountValue =>
        _snapshot.Value?.LayoutNodeCount.ToString("N0") ?? "—";

    public string HitRegionCountValue =>
        _snapshot.Value?.HitRegionCount.ToString("N0") ?? "—";

    public string RetainedText => _snapshot.Value is { } snapshot
        ? $"{snapshot.LayoutNodeCount:N0} nodes · {snapshot.HitRegionCount:N0} hit regions"
        : "no layout yet";

    public string ColorVisionValue =>
        _snapshot.Value?.VisualAccessibility.ColorVision.ToString() ?? "—";

    public string HighContrastValue => _snapshot.Value is { } snapshot
        ? snapshot.VisualAccessibility.HighContrast ? "Enabled" : "Disabled"
        : "—";

    public string LayoutTimingClass => TimingClass(_snapshot.Value?.LayoutMilliseconds);

    public string ComposeTimingClass => TimingClass(_snapshot.Value?.ComposeMilliseconds);

    public string RenderTimingClass => TimingClass(_snapshot.Value?.RenderMilliseconds);

    private double FrameMilliseconds => _snapshot.Value is { } snapshot
        ? snapshot.LayoutMilliseconds +
          snapshot.ComposeMilliseconds +
          snapshot.RenderMilliseconds
        : 0;

    public string FrameBudgetText => _snapshot.Value is null
        ? $"— / {FrameBudgetMilliseconds:0.0} ms"
        : $"{FrameMilliseconds:0.00} / {FrameBudgetMilliseconds:0.0} ms";

    public Length FrameBudgetWidth => Length.Percent((float)Math.Clamp(
        FrameMilliseconds / FrameBudgetMilliseconds * 100,
        0,
        100));

    public string FrameBudgetClass => FrameMilliseconds > FrameBudgetMilliseconds
        ? "budget-fill danger"
        : FrameMilliseconds > FrameBudgetMilliseconds * 0.75
            ? "budget-fill warning"
            : "budget-fill normal";

    public string FrameText => _snapshot.Value is { } snapshot
        ? $"Frame {snapshot.FrameNumber:N0} · Layout v{snapshot.LayoutVersion}"
        : "Waiting for diagnostics";

    public string LayoutText => FormatMilliseconds(
        _snapshot.Value?.LayoutMilliseconds);

    public string ComposeText => FormatMilliseconds(
        _snapshot.Value?.ComposeMilliseconds);

    public string RenderText => FormatMilliseconds(
        _snapshot.Value?.RenderMilliseconds);

    public string DamageText => _snapshot.Value is { } snapshot
        ? $"{snapshot.DamageRects:N0} rects · {snapshot.DamageArea:N0} px²"
        : "—";

    public string PaintCacheText => _snapshot.Value is { } snapshot
        ? $"{snapshot.ReusedPaintBoundaries:N0} reused · " +
          $"{snapshot.RepaintedPaintBoundaries:N0} repainted"
        : "—";

    public string RelayoutCacheText => _snapshot.Value is { } snapshot
        ? $"{InvalidationScopeValue} · {snapshot.InvalidationPathNodeCount:N0} nodes"
        : "—";

    public string VisionText => _snapshot.Value is { } snapshot
        ? $"{snapshot.VisualAccessibility.ColorVision} · " +
          $"high contrast {snapshot.VisualAccessibility.HighContrast}"
        : "—";

    private static string FormatMilliseconds(double? value) =>
        value is { } milliseconds ? $"{milliseconds:0.00} ms" : "—";

    private static string TimingClass(double? value) {
        if (value > FrameBudgetMilliseconds) {
            return "metric danger";
        }

        if (value > FrameBudgetMilliseconds * 0.75) {
            return "metric warning";
        }

        return "metric";
    }

    private static string FormatHitRate(int hits, int misses) {
        var total = hits + misses;
        return total == 0
            ? "No requests"
            : $"{100d * hits / total:0}% hit · {hits:N0}/{total:N0}";
    }
}
