namespace Melanopsin.UI.Layout;

internal readonly record struct CachedMeasurement(
    long LayoutRevision,
    long StyleSheetVersion,
    LayoutConstraints Constraints,
    UiMetrics Metrics,
    bool UsesViewportUnits,
    LayoutSize Size) {
    public bool IsValid(
        Node node,
        long styleSheetVersion,
        LayoutConstraints constraints,
        UiMetrics metrics) {
        return LayoutRevision == node.SubtreeLayoutRevision &&
               StyleSheetVersion == styleSheetVersion &&
               Constraints == constraints &&
               Metrics.HasSameCacheIdentity(metrics, UsesViewportUnits);
    }
}
