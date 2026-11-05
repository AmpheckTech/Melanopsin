// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable closed polygon in two-dimensional Cartesian space.
/// </summary>
/// <remarks>
/// Vertices are stored once in boundary order; the closing edge from the last
/// vertex to the first is implicit. The polygon may be concave. Containment
/// uses the even-odd rule, which is also defined for self-intersecting input,
/// while area and centroid use the algebraic shoelace convention. Callers that
/// require simple-polygon semantics should validate their input separately.
/// </remarks>
public sealed partial class Polygon2 {
    private readonly Vector2[] _vertices;

    public Polygon2(ReadOnlySpan<Vector2> vertices) {
        if (vertices.Length < 3) {
            throw new ArgumentException(
                "A polygon requires at least three vertices.",
                nameof(vertices));
        }

        _vertices = vertices.ToArray();
        for (var index = 0; index < _vertices.Length; index++) {
            if (!_vertices[index].IsFinite) {
                throw new ArgumentException(
                    "Polygon vertices must be finite.",
                    nameof(vertices));
            }
        }
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;

    public int VertexCount => _vertices.Length;

    /// <summary>
    /// Gets the signed shoelace area. Positive orientation is counterclockwise
    /// in a conventional Cartesian X-right, Y-up plane.
    /// </summary>
    public float SignedArea {
        get {
            return (float)(0.5 * ComputeTwiceSignedArea());
        }
    }

    public float Area => MathF.Abs(SignedArea);

    public float Perimeter {
        get {
            var result = 0.0;
            for (var index = 0; index < _vertices.Length; index++) {
                result += GeometryPredicates.Distance(
                    _vertices[index],
                    _vertices[(index + 1) % _vertices.Length]);
            }

            return (float)result;
        }
    }

    public Aabb2 Bounds => Aabb2.FromPoints(_vertices);

    public bool IsCounterClockwise => SignedArea > 0;

    /// <summary>
    /// Gets the area centroid, or a vector whose components are NaN when the
    /// algebraic area is zero or the moment is not representable as a finite
    /// single-precision value.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TryGetCentroid(out Vector2, out GeometryOperationReport)"/>
    /// when the reason for failure is required.
    /// </remarks>
    public Vector2 Centroid {
        get {
            return TryGetCentroid(out var centroid)
                ? centroid
                : new Vector2(float.NaN, float.NaN);
        }
    }

    public bool TryGetCentroid(out Vector2 centroid) {
        return TryGetCentroid(out centroid, out _);
    }

    /// <summary>
    /// Attempts to compute the area centroid and reports a zero-area or
    /// non-finite polygon moment.
    /// </summary>
    public bool TryGetCentroid(
        out Vector2 centroid,
        out GeometryOperationReport report) {
        var twiceArea = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;
        for (var index = 0; index < _vertices.Length; index++) {
            var next = (index + 1) % _vertices.Length;
            var first = _vertices[index];
            var second = _vertices[next];
            var cross =
                ((double)first.X * second.Y) -
                ((double)second.X * first.Y);
            twiceArea += cross;
            weightedX += ((double)first.X + second.X) * cross;
            weightedY += ((double)first.Y + second.Y) * cross;
        }

        if (!double.IsFinite(twiceArea) ||
            !double.IsFinite(weightedX) ||
            !double.IsFinite(weightedY)) {
            centroid = default;
            report = new GeometryOperationReport(
                GeometryOperation.PolygonCentroid,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (twiceArea == 0) {
            centroid = default;
            report = new GeometryOperationReport(
                GeometryOperation.PolygonCentroid,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var scale = 1 / (3 * twiceArea);
        var x = weightedX * scale;
        var y = weightedY * scale;
        if (!MatrixNumerics.IsRepresentableAsFloat(x) ||
            !MatrixNumerics.IsRepresentableAsFloat(y)) {
            centroid = default;
            report = new GeometryOperationReport(
                GeometryOperation.PolygonCentroid,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        centroid = new Vector2((float)x, (float)y);
        report = new GeometryOperationReport(
            GeometryOperation.PolygonCentroid,
            GeometryStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Tests closed-polygon containment using the even-odd rule.
    /// </summary>
    /// <remarks>
    /// Points exactly on an edge or vertex count as contained. No tolerance is
    /// applied; callers seeking approximate geometric predicates should
    /// normalize or snap their input before calling this method.
    /// </remarks>
    public bool Contains(Vector2 point) {
        if (!point.IsFinite) {
            return false;
        }

        var inside = false;
        for (var firstIndex = 0; firstIndex < _vertices.Length; firstIndex++) {
            var secondIndex = (firstIndex + 1) % _vertices.Length;
            var first = _vertices[firstIndex];
            var second = _vertices[secondIndex];
            if (IsOnSegment(point, first, second)) {
                return true;
            }

            var crossesScanline = (first.Y > point.Y) != (second.Y > point.Y);
            if (!crossesScanline) {
                continue;
            }

            var intersectionX =
                ((((double)second.X - first.X) * ((double)point.Y - first.Y)) /
                    ((double)second.Y - first.Y)) + first.X;
            if (point.X < intersectionX) {
                inside = !inside;
            }
        }

        return inside;
    }

    public Vector2 ClosestPoint(Vector2 point) {
        if (!point.IsFinite) {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                "The query point must be finite.");
        }

        if (Contains(point)) {
            return point;
        }

        var closest = _vertices[0];
        var closestDistanceSquared = double.PositiveInfinity;
        for (var index = 0; index < _vertices.Length; index++) {
            var candidate = ClosestPointOnSegment(
                point,
                _vertices[index],
                _vertices[(index + 1) % _vertices.Length]);
            var distanceSquared = GeometryPredicates.DistanceSquared(point, candidate);
            if (distanceSquared < closestDistanceSquared) {
                closest = candidate;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closest;
    }

    public float DistanceSquared(Vector2 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector2 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    public Polygon2 Reversed() {
        var reversed = _vertices.AsSpan().ToArray();
        Array.Reverse(reversed);
        return new Polygon2(reversed);
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end) {
        double directionX = (double)end.X - start.X;
        double directionY = (double)end.Y - start.Y;
        var lengthSquared = (directionX * directionX) + (directionY * directionY);
        if (lengthSquared == 0) {
            return start;
        }

        var amount = ScalarMath.Clamp(
            ((((double)point.X - start.X) * directionX) +
                (((double)point.Y - start.Y) * directionY)) / lengthSquared,
            0,
            1);
        return new Vector2(
            (float)(start.X + (amount * directionX)),
            (float)(start.Y + (amount * directionY)));
    }

    private static bool IsOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end) {
        var cross = GeometryPredicates.Cross(start, end, point);
        if (cross != 0) {
            return false;
        }

        return point.X >= MathF.Min(start.X, end.X) &&
            point.X <= MathF.Max(start.X, end.X) &&
            point.Y >= MathF.Min(start.Y, end.Y) &&
            point.Y <= MathF.Max(start.Y, end.Y);
    }
}
