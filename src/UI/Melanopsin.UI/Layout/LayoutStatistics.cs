// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Layout;

/// <summary>
/// Per-pass layout work counters, reset at the start of each
/// <see cref="ILayoutEngine.Layout"/> call. Diagnostics only.
/// </summary>
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
