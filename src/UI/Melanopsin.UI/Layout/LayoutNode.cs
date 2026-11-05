using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

public sealed class LayoutNode {
    internal LayoutNode(
        Node node,
        Rect bounds,
        LayoutNode? parent,
        Rect? clipBounds,
        LayoutConstraints? measureConstraints,
        long arrangementRevision,
        long styleSheetVersion,
        UiMetrics metrics,
        bool usesViewportUnits) {
        Node = node;
        Bounds = bounds;
        Parent = parent;
        ClipBounds = clipBounds;
        MeasureConstraints = measureConstraints;
        ArrangementRevision = arrangementRevision;
        StyleSheetVersion = styleSheetVersion;
        Metrics = metrics;
        UsesViewportUnits = usesViewportUnits;
    }

    public Node Node { get; }

    public Rect Bounds { get; internal set; }

    public LayoutNode? Parent { get; internal set; }

    public Rect? ClipBounds { get; internal set; }

    /// <summary>
    /// The constraints supplied by the parent during the latest measure of
    /// this node, or <see langword="null"/> when the node has not been measured.
    /// </summary>
    public LayoutConstraints? MeasureConstraints { get; internal set; }

    public List<LayoutNode> Children { get; } = [];

    public int ZIndex => Node is Control control ? control.ZIndex : 0;

    internal int SiblingIndex { get; set; }

    internal bool WasArrangementReused { get; private set; }

    internal HitRegion? CachedHitRegion { get; set; }

    internal HitRegion[]? CachedHitRegions { get; set; }

    private long ArrangementRevision { get; set; }

    private long StyleSheetVersion { get; set; }

    private UiMetrics Metrics { get; set; }

    private bool UsesViewportUnits { get; set; }

    internal bool TryReuseArrangement(
        Rect bounds,
        LayoutNode? parent,
        Rect? clipBounds,
        LayoutConstraints? measureConstraints,
        long arrangementRevision,
        long styleSheetVersion,
        UiMetrics metrics,
        bool usesViewportUnits) {
        if (ArrangementRevision != arrangementRevision ||
            StyleSheetVersion != styleSheetVersion ||
            Bounds != bounds ||
            !ReferenceEquals(Parent, parent) ||
            ClipBounds != clipBounds ||
            MeasureConstraints != measureConstraints ||
            !Metrics.HasSameCacheIdentity(metrics, UsesViewportUnits)) {
            return false;
        }

        WasArrangementReused = true;
        return true;
    }

    internal void Update(
        Rect bounds,
        LayoutNode? parent,
        Rect? clipBounds,
        LayoutConstraints? measureConstraints,
        long arrangementRevision,
        long styleSheetVersion,
        UiMetrics metrics,
        bool usesViewportUnits) {
        Bounds = bounds;
        Parent = parent;
        ClipBounds = clipBounds;
        MeasureConstraints = measureConstraints;
        ArrangementRevision = arrangementRevision;
        StyleSheetVersion = styleSheetVersion;
        Metrics = metrics;
        UsesViewportUnits = usesViewportUnits;
        WasArrangementReused = false;
        CachedHitRegion = null;
        CachedHitRegions = null;
        Children.Clear();
    }
}
