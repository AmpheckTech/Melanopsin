namespace Melanopsin.UI.Layout;

/// <summary>
/// Per-pass layout work counters. Counters are reset at the start of each
/// <see cref="ILayoutEngine.Layout"/> call and are intended for diagnostics,
/// regression comparison and tests rather than application control flow.
/// </summary>
/// <param name="MeasureRequests">
/// Calls to <see cref="FlowLayoutEngine.MeasureNode"/>, including cache hits.
/// </param>
/// <param name="MeasureCacheHits">
/// Measurement requests satisfied by the current-pass measurement cache.
/// </param>
/// <param name="RelayoutBoundaryHits">
/// Persistent measurement-cache hits for nodes marked as relayout boundaries.
/// </param>
/// <param name="RelayoutBoundaryMisses">
/// Measurement executions for nodes marked as relayout boundaries.
/// </param>
/// <param name="ArrangeReuseHits">
/// Arrangement subtrees reused without rebuilding their descendants.
/// </param>
/// <param name="ArrangeReuseMisses">
/// Layout nodes whose arrangement was created or rebuilt. A hit at a stable
/// subtree root does not count its descendants.
/// </param>
/// <param name="HitRegionCacheHits">
/// Hit regions reused from a cached arrangement subtree.
/// </param>
/// <param name="HitRegionCacheMisses">
/// Hit regions created or updated because their cache entry was not reusable.
/// </param>
/// <param name="MeasureExecutions">
/// Measurement calculations that missed both the current-pass and persistent
/// measurement caches.
/// </param>
/// <param name="PersistentMeasureHits">
/// Measurement requests satisfied by the persistent cross-pass cache.
/// </param>
/// <param name="InvalidationScope">
/// Whether the pass attributed layout changes to merged root-to-source paths
/// or had to use the full-invalidation fallback.
/// </param>
/// <param name="InvalidationPathNodeCount">
/// Number of unique nodes in the merged root-to-source attribution paths. This
/// is zero when no safe path was available.
/// </param>
public readonly record struct LayoutStatistics(
    int MeasureRequests,
    int MeasureCacheHits,
    int RelayoutBoundaryHits,
    int RelayoutBoundaryMisses,
    int ArrangeReuseHits = 0,
    int ArrangeReuseMisses = 0,
    int HitRegionCacheHits = 0,
    int HitRegionCacheMisses = 0,
    int MeasureExecutions = 0,
    int PersistentMeasureHits = 0,
    LayoutInvalidationScope InvalidationScope = LayoutInvalidationScope.None,
    int InvalidationPathNodeCount = 0);
