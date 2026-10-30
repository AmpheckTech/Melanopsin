using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

internal sealed class CachedStyle {
    public CachedStyle(
        long nodeStyleRevision,
        long sheetVersion,
        UiMetrics metrics,
        ResolvedStyle style,
        ResolvedStyle? pendingTransitionSource = null) {
        NodeStyleRevision = nodeStyleRevision;
        SheetVersion = sheetVersion;
        Metrics = metrics;
        Style = style;
        PendingTransitionSource = pendingTransitionSource;
    }

    private long NodeStyleRevision { get; }

    private long SheetVersion { get; }

    private UiMetrics Metrics { get; }

    public ResolvedStyle Style { get; }

    public ResolvedStyle? PendingTransitionSource { get; private set; }

    public bool IsValid(
        Node node,
        StyleSheet sheet,
        UiMetrics metrics) {
        return NodeStyleRevision == node.StyleRevision &&
               SheetVersion == sheet.Version &&
               Metrics.HasSameCacheIdentity(metrics, Style.UsesViewportUnits);
    }

    public ResolvedStyle? TakePendingTransitionSource() {
        var source = PendingTransitionSource;
        PendingTransitionSource = null;
        return source;
    }

    public void ClearPendingTransitionSource() {
        PendingTransitionSource = null;
    }
}
