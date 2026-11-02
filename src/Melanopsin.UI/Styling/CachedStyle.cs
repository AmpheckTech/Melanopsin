using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

internal sealed class CachedStyle {
    private readonly Node[] _ancestorNodes;
    private readonly long[] _ancestorStyleRevisions;

    public CachedStyle(
        Node node,
        long sheetVersion,
        UiMetrics metrics,
        ResolvedStyle style,
        ResolvedStyle? pendingTransitionSource = null) {
        NodeStyleRevision = node.StyleRevision;
        SheetVersion = sheetVersion;
        Metrics = metrics;
        Style = style;
        PendingTransitionSource = pendingTransitionSource;

        var ancestorCount = 0;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
            ancestorCount++;
        }

        _ancestorNodes = new Node[ancestorCount];
        _ancestorStyleRevisions = new long[ancestorCount];
        var index = 0;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
            _ancestorNodes[index] = ancestor;
            _ancestorStyleRevisions[index] = ancestor.StyleRevision;
            index++;
        }
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
        if (NodeStyleRevision != node.StyleRevision ||
            SheetVersion != sheet.Version ||
            !Metrics.HasSameCacheIdentity(metrics, Style.UsesViewportUnits)) {
            return false;
        }

        var index = 0;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
            if (index >= _ancestorNodes.Length ||
                !ReferenceEquals(_ancestorNodes[index], ancestor) ||
                _ancestorStyleRevisions[index] != ancestor.StyleRevision) {
                return false;
            }

            index++;
        }

        return index == _ancestorNodes.Length;
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
