namespace Melanopsin.Mathematics;

/// <summary>
/// Planar convex-hull helpers.
/// </summary>
/// <remarks>
/// <see cref="Compute"/> implements the monotone-chain algorithm of Andrew
/// (1979),
/// <see href="https://doi.org/10.1016/0020-0190(79)90072-3">doi:10.1016/0020-0190(79)90072-3</see>.
/// </remarks>
public static class ConvexHull2 {
    /// <summary>
    /// Computes the counterclockwise convex hull of finite planar points.
    /// </summary>
    /// <remarks>
    /// Duplicate points are removed. Collinear boundary points between hull
    /// extremes are omitted. One or two distinct input points produce a
    /// one-point or two-point hull respectively. No closing duplicate is added.
    /// The algorithm uses exact comparisons and no tolerance.
    /// </remarks>
    public static Vector2[] Compute(ReadOnlySpan<Vector2> points) {
        if (points.IsEmpty) {
            throw new ArgumentException(
                "At least one point is required.",
                nameof(points));
        }

        for (var index = 0; index < points.Length; index++) {
            if (!points[index].IsFinite) {
                throw new ArgumentException(
                    "All points must be finite.",
                    nameof(points));
            }
        }

        var sorted = points.ToArray();
        Array.Sort(sorted, ComparePoints);
        var uniqueCount = 1;
        for (var index = 1; index < sorted.Length; index++) {
            if (sorted[index] != sorted[uniqueCount - 1]) {
                sorted[uniqueCount++] = sorted[index];
            }
        }

        if (uniqueCount <= 2) {
            return sorted.AsSpan(0, uniqueCount).ToArray();
        }

        var hull = new List<Vector2>(uniqueCount * 2);
        for (var index = 0; index < uniqueCount; index++) {
            while (hull.Count >= 2 && Cross(
                hull[^2],
                hull[^1],
                sorted[index]) <= 0) {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(sorted[index]);
        }

        var lowerCount = hull.Count;
        for (var index = uniqueCount - 2; index >= 0; index--) {
            while (hull.Count > lowerCount && Cross(
                hull[^2],
                hull[^1],
                sorted[index]) <= 0) {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(sorted[index]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }

    private static int ComparePoints(Vector2 left, Vector2 right) {
        var xComparison = left.X.CompareTo(right.X);
        return xComparison != 0 ? xComparison : left.Y.CompareTo(right.Y);
    }

    private static double Cross(Vector2 first, Vector2 second, Vector2 third) {
        return GeometryPredicates.Cross(first, second, third);
    }
}
