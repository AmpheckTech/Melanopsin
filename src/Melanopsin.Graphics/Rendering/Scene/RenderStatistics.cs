namespace Melanopsin.Graphics.Rendering;

public readonly record struct RenderStatistics(
    int CommandCount,
    int ReusedPaintBoundaries,
    int RepaintedPaintBoundaries,
    int RasterCacheHits = 0,
    int RasterCacheMisses = 0,
    long RasterCacheBytes = 0);
