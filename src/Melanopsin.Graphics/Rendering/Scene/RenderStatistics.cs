// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

public readonly record struct RenderStatistics(
    int CommandCount,
    int ReusedPaintBoundaries,
    int RepaintedPaintBoundaries,
    int RasterCacheHits = 0,
    int RasterCacheMisses = 0,
    long RasterCacheBytes = 0);
