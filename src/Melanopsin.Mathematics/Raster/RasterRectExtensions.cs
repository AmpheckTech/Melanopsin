namespace Melanopsin.Mathematics.Raster;

/// <summary>
/// Interprets convention-neutral rectangle bounds in raster coordinates.
/// </summary>
public static class RasterRectExtensions {
    public static Point GetTopLeft(this Rect rect, RasterOrigin origin) {
        return origin switch {
            RasterOrigin.TopLeft => new Point(rect.MinX, rect.MinY),
            RasterOrigin.BottomLeft => new Point(rect.MinX, rect.MaxY),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }

    public static Point GetBottomLeft(this Rect rect, RasterOrigin origin) {
        return origin switch {
            RasterOrigin.TopLeft => new Point(rect.MinX, rect.MaxY),
            RasterOrigin.BottomLeft => new Point(rect.MinX, rect.MinY),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }

    public static Point GetTopRight(this Rect rect, RasterOrigin origin) {
        return origin switch {
            RasterOrigin.TopLeft => new Point(rect.MaxX, rect.MinY),
            RasterOrigin.BottomLeft => new Point(rect.MaxX, rect.MaxY),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }

    public static Point GetBottomRight(this Rect rect, RasterOrigin origin) {
        return origin switch {
            RasterOrigin.TopLeft => new Point(rect.MaxX, rect.MaxY),
            RasterOrigin.BottomLeft => new Point(rect.MaxX, rect.MinY),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }
}
