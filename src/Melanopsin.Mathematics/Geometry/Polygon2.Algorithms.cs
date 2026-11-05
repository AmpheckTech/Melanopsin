// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace Melanopsin.Mathematics;

public sealed partial class Polygon2 {
    /// <summary>
    /// Determines whether the polygon boundary is simple.
    /// </summary>
    /// <remarks>
    /// Adjacent edges may share their common endpoint. Any non-adjacent edge
    /// crossing, endpoint contact, or collinear overlap makes the polygon
    /// non-simple. No geometric tolerance is applied; callers must snap or
    /// normalize near-degenerate input according to their own requirements.
    /// </remarks>
    public bool IsSimple {
        get {
            for (var firstIndex = 0; firstIndex < _vertices.Length; firstIndex++) {
                var firstNext = (firstIndex + 1) % _vertices.Length;
                if (_vertices[firstIndex] == _vertices[firstNext]) {
                    return false;
                }

                for (var secondIndex = firstIndex + 1;
                    secondIndex < _vertices.Length;
                    secondIndex++) {
                    var secondNext = (secondIndex + 1) % _vertices.Length;
                    if (secondIndex == firstNext ||
                        (firstIndex == 0 &&
                            secondNext == 0)) {
                        continue;
                    }

                    if (SegmentsIntersect(
                        _vertices[firstIndex],
                        _vertices[firstNext],
                        _vertices[secondIndex],
                        _vertices[secondNext])) {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Triangulates a simple polygon with ear clipping.
    /// </summary>
    /// <remarks>
    /// The polygon must be simple and have a non-zero signed area. Returned
    /// triangles preserve the input winding. Collinear boundary vertices that
    /// do not affect the covered area are omitted. Failure to find an ear
    /// violates the simple-polygon contract and throws
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    public Triangle2[] Triangulate() {
        if (!TryTriangulate(out var triangles, out var report)) {
            throw new ArithmeticException(
                $"The polygon could not be triangulated: {report.Status}.");
        }

        return triangles;
    }

    /// <summary>
    /// Attempts to triangulate a simple polygon and reports invalid topology,
    /// zero area, or a non-finite intermediate.
    /// </summary>
    /// <remarks>
    /// This convenience overload allocates the result array. This polygon is
    /// considered simple only when all edges have non-zero length and no
    /// non-adjacent edges intersect. No approximate geometric tolerance is
    /// applied.
    /// </remarks>
    public bool TryTriangulate(
        out Triangle2[] triangles,
        out GeometryOperationReport report) {
        var triangleCapacity = _vertices.Length - 2;
        var indices = ArrayPool<int>.Shared.Rent(_vertices.Length);
        var output = ArrayPool<Triangle2>.Shared.Rent(triangleCapacity);
        try {
            if (!TryTriangulate(
                    indices.AsSpan(0, _vertices.Length),
                    output.AsSpan(0, triangleCapacity),
                    out var triangleCount,
                    out report)) {
                triangles = [];
                return false;
            }

            triangles = output.AsSpan(0, triangleCount).ToArray();
            return true;
        } finally {
            ArrayPool<int>.Shared.Return(indices);
            ArrayPool<Triangle2>.Shared.Return(output, clearArray: true);
        }
    }

    /// <summary>
    /// Attempts to triangulate a simple polygon without allocating.
    /// </summary>
    /// <param name="indexWorkspace">
    /// Caller-owned index scratch space with at least <see cref="VertexCount"/>
    /// elements.
    /// </param>
    /// <param name="triangleWorkspace">
    /// Caller-owned output storage with at least
    /// <see cref="VertexCount"/> minus two elements.
    /// </param>
    /// <param name="triangleCount">
    /// Receives the number of triangles written to the prefix of
    /// <paramref name="triangleWorkspace"/>.
    /// </param>
    /// <param name="report">The operation report.</param>
    /// <remarks>
    /// Insufficient workspace is a programming error and throws
    /// <see cref="ArgumentException"/>. Invalid topology returns
    /// <see cref="GeometryStatus.InvalidInput"/>; a zero signed area returns
    /// <see cref="GeometryStatus.DegenerateGeometry"/>; and a non-finite area or
    /// turn test returns <see cref="GeometryStatus.NonFiniteOutput"/>.
    /// </remarks>
    public bool TryTriangulate(
        Span<int> indexWorkspace,
        Span<Triangle2> triangleWorkspace,
        out int triangleCount,
        out GeometryOperationReport report) {
        if (indexWorkspace.Length < _vertices.Length) {
            throw new ArgumentException(
                $"Triangulation requires at least {_vertices.Length} index values.",
                nameof(indexWorkspace));
        }

        var requiredTriangleCapacity = _vertices.Length - 2;
        if (triangleWorkspace.Length < requiredTriangleCapacity) {
            throw new ArgumentException(
                $"Triangulation requires at least {requiredTriangleCapacity} triangle values.",
                nameof(triangleWorkspace));
        }

        triangleCount = 0;
        if (!IsSimple) {
            report = new GeometryOperationReport(
                GeometryOperation.PolygonTriangulation,
                GeometryStatus.InvalidInput);
            return false;
        }

        var twiceSignedArea = ComputeTwiceSignedArea();
        if (!double.IsFinite(twiceSignedArea)) {
            report = new GeometryOperationReport(
                GeometryOperation.PolygonTriangulation,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (twiceSignedArea == 0) {
            report = new GeometryOperationReport(
                GeometryOperation.PolygonTriangulation,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var orientation = Math.Sign(twiceSignedArea);
        var indices = indexWorkspace[.._vertices.Length];
        for (var index = 0; index < indices.Length; index++) {
            indices[index] = index;
        }

        var activeCount = indices.Length;
        while (activeCount > 3) {
            var progressed = false;
            for (var index = 0; index < activeCount; index++) {
                var previousIndex = indices[
                    (index + activeCount - 1) % activeCount];
                var currentIndex = indices[index];
                var nextIndex = indices[(index + 1) % activeCount];
                var previous = _vertices[previousIndex];
                var current = _vertices[currentIndex];
                var next = _vertices[nextIndex];
                var turn = CrossDouble(current - previous, next - current);
                if (!double.IsFinite(turn)) {
                    report = new GeometryOperationReport(
                        GeometryOperation.PolygonTriangulation,
                        GeometryStatus.NonFiniteOutput);
                    return false;
                }

                if (turn == 0) {
                    RemoveIndex(indices, activeCount, index);
                    activeCount--;
                    progressed = true;
                    break;
                }

                if (orientation > 0 ? turn < 0 : turn > 0) {
                    continue;
                }

                if (ContainsOtherVertex(
                    indices,
                    activeCount,
                    previousIndex,
                    currentIndex,
                    nextIndex,
                    previous,
                    current,
                    next,
                    orientation)) {
                    continue;
                }

                triangleWorkspace[triangleCount++] =
                    new Triangle2(previous, current, next);
                RemoveIndex(indices, activeCount, index);
                activeCount--;
                progressed = true;
                break;
            }

            if (!progressed) {
                report = new GeometryOperationReport(
                    GeometryOperation.PolygonTriangulation,
                    GeometryStatus.NumericalFailure);
                return false;
            }
        }

        triangleWorkspace[triangleCount++] = new Triangle2(
            _vertices[indices[0]],
            _vertices[indices[1]],
            _vertices[indices[2]]);
        report = new GeometryOperationReport(
            GeometryOperation.PolygonTriangulation,
            GeometryStatus.Succeeded);
        return true;
    }

    private double ComputeTwiceSignedArea() {
        var result = 0.0;
        for (var index = 0; index < _vertices.Length; index++) {
            var next = (index + 1) % _vertices.Length;
            result +=
                ((double)_vertices[index].X * _vertices[next].Y) -
                ((double)_vertices[next].X * _vertices[index].Y);
        }

        return result;
    }

    private bool ContainsOtherVertex(
        ReadOnlySpan<int> indices,
        int activeCount,
        int previousIndex,
        int currentIndex,
        int nextIndex,
        Vector2 previous,
        Vector2 current,
        Vector2 next,
        int orientation) {
        for (var other = 0; other < activeCount; other++) {
            var otherIndex = indices[other];
            if (otherIndex == previousIndex ||
                otherIndex == currentIndex ||
                otherIndex == nextIndex) {
                continue;
            }

            if (IsInsideOrOnTriangle(
                _vertices[otherIndex],
                previous,
                current,
                next,
                orientation)) {
                return true;
            }
        }

        return false;
    }

    private static void RemoveIndex(
        Span<int> indices,
        int activeCount,
        int index) {
        indices[(index + 1)..activeCount].CopyTo(indices[index..]);
    }

    private static bool IsInsideOrOnTriangle(
        Vector2 point,
        Vector2 first,
        Vector2 second,
        Vector2 third,
        int orientation) {
        var firstCross = CrossDouble(second - first, point - first);
        var secondCross = CrossDouble(third - second, point - second);
        var thirdCross = CrossDouble(first - third, point - third);
        return orientation > 0
            ? firstCross >= 0 && secondCross >= 0 && thirdCross >= 0
            : firstCross <= 0 && secondCross <= 0 && thirdCross <= 0;
    }

    private static double CrossDouble(Vector2 left, Vector2 right) {
        return ((double)left.X * right.Y) - ((double)left.Y * right.X);
    }

    private static bool SegmentsIntersect(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd) {
        var firstOrientation = CrossDouble(
            firstEnd - firstStart,
            secondStart - firstStart);
        var secondOrientation = CrossDouble(
            firstEnd - firstStart,
            secondEnd - firstStart);
        var thirdOrientation = CrossDouble(
            secondEnd - secondStart,
            firstStart - secondStart);
        var fourthOrientation = CrossDouble(
            secondEnd - secondStart,
            firstEnd - secondStart);
        if (firstOrientation == 0 && IsWithinSegmentBounds(
            secondStart,
            firstStart,
            firstEnd)) {
            return true;
        }

        if (secondOrientation == 0 && IsWithinSegmentBounds(
            secondEnd,
            firstStart,
            firstEnd)) {
            return true;
        }

        if (thirdOrientation == 0 && IsWithinSegmentBounds(
            firstStart,
            secondStart,
            secondEnd)) {
            return true;
        }

        if (fourthOrientation == 0 && IsWithinSegmentBounds(
            firstEnd,
            secondStart,
            secondEnd)) {
            return true;
        }

        return (firstOrientation > 0) != (secondOrientation > 0) &&
            (thirdOrientation > 0) != (fourthOrientation > 0);
    }

    private static bool IsWithinSegmentBounds(
        Vector2 point,
        Vector2 start,
        Vector2 end) {
        return point.X >= MathF.Min(start.X, end.X) &&
            point.X <= MathF.Max(start.X, end.X) &&
            point.Y >= MathF.Min(start.Y, end.Y) &&
            point.Y <= MathF.Max(start.Y, end.Y);
    }
}
