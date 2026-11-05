// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Rendering;

public readonly record struct DevToolsSnapshot(
    long FrameNumber,
    long LayoutVersion,
    int CommandCount,
    int DamageRects,
    long DamageArea,
    int ReusedPaintBoundaries,
    int RepaintedPaintBoundaries,
    int RasterCacheHits,
    int RasterCacheMisses,
    long RasterCacheBytes,
    LayoutInvalidationScope InvalidationScope,
    int InvalidationPathNodeCount,
    int RelayoutBoundaryHits,
    int RelayoutBoundaryMisses,
    int ArrangeReuseHits,
    int ArrangeReuseMisses,
    int HitRegionCacheHits,
    int HitRegionCacheMisses,
    int MeasureExecutions,
    int PersistentMeasureHits,
    int LayoutNodeCount,
    int HitRegionCount,
    double LayoutMilliseconds,
    double ComposeMilliseconds,
    double RenderMilliseconds,
    AccessibilityVisualOptions VisualAccessibility);
