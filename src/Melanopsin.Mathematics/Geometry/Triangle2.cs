namespace Melanopsin.Mathematics;

/// <summary>
/// A triangle in the two-dimensional Cartesian plane.
/// </summary>
/// <remarks>
/// Degenerate triangles are representable. Signed area is positive for
/// counterclockwise vertex order in the conventional X-right, Y-up plane.
/// Barycentric operations of a degenerate triangle return
/// <see langword="false"/> rather than using an absolute area threshold.
/// </remarks>
public readonly partial record struct Triangle2 {
    public Triangle2(Vector2 first, Vector2 second, Vector2 third) {
        if (!first.IsFinite || !second.IsFinite || !third.IsFinite) {
            throw new ArgumentException(
                "Triangle vertices must be finite.",
                nameof(first));
        }

        First = first;
        Second = second;
        Third = third;
    }

    public Vector2 First { get; }

    public Vector2 Second { get; }

    public Vector2 Third { get; }

    public Vector2 Centroid => new(
        (float)(((double)First.X + Second.X + Third.X) / 3),
        (float)(((double)First.Y + Second.Y + Third.Y) / 3));

    public double SignedArea =>
        0.5 * GeometryPredicates.Cross(First, Second, Third);

    public float Area => (float)Math.Abs(SignedArea);

    public Aabb2 Bounds => Aabb2.FromPoints([First, Second, Third]);

    public bool TryGetBarycentric(Vector2 point, out Vector3 barycentric) {
        return TryGetBarycentric(point, out barycentric, out _);
    }

    /// <summary>
    /// Attempts to evaluate barycentric coordinates and reports non-finite
    /// input or a degenerate triangle.
    /// </summary>
    public bool TryGetBarycentric(
        Vector2 point,
        out Vector3 barycentric,
        out GeometryOperationReport report) {
        if (!point.IsFinite) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        var twiceArea = GeometryPredicates.Cross(First, Second, Third);
        if (!double.IsFinite(twiceArea)) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (twiceArea == 0) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var secondWeight = GeometryPredicates.Cross(First, point, Third) /
            twiceArea;
        var thirdWeight = GeometryPredicates.Cross(First, Second, point) /
            twiceArea;
        barycentric = new Vector3(
            (float)(1 - secondWeight - thirdWeight),
            (float)secondWeight,
            (float)thirdWeight);
        if (!barycentric.IsFinite) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        report = new GeometryOperationReport(
            GeometryOperation.TriangleBarycentricCoordinates,
            GeometryStatus.Succeeded);
        return true;
    }

    public Vector3 Barycentric(Vector2 point) {
        if (!TryGetBarycentric(point, out var result)) {
            throw new ArithmeticException(
                "Barycentric coordinates are undefined for a degenerate triangle.");
        }

        return result;
    }

    /// <summary>
    /// Tests containment in the closed triangle using an explicit barycentric
    /// tolerance. The default is zero.
    /// </summary>
    public bool Contains(Vector2 point, float tolerance = 0) {
        if (!float.IsFinite(tolerance) || tolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        if (!TryGetBarycentric(point, out var weights)) {
            return false;
        }

        return weights.X >= -tolerance &&
            weights.Y >= -tolerance &&
            weights.Z >= -tolerance &&
            weights.X <= 1 + tolerance &&
            weights.Y <= 1 + tolerance &&
            weights.Z <= 1 + tolerance;
    }

    public Vector2 ClosestPoint(Vector2 point) {
        if (Contains(point)) {
            return point;
        }

        var firstCandidate = ClosestPointOnSegment(point, First, Second);
        var secondCandidate = ClosestPointOnSegment(point, Second, Third);
        var thirdCandidate = ClosestPointOnSegment(point, Third, First);
        var firstDistance = GeometryPredicates.DistanceSquared(point, firstCandidate);
        var secondDistance = GeometryPredicates.DistanceSquared(point, secondCandidate);
        var thirdDistance = GeometryPredicates.DistanceSquared(point, thirdCandidate);
        if (firstDistance <= secondDistance && firstDistance <= thirdDistance) {
            return firstCandidate;
        }

        return secondDistance <= thirdDistance ? secondCandidate : thirdCandidate;
    }

    public float DistanceSquared(Vector2 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector2 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end) {
        double directionX = (double)end.X - start.X;
        double directionY = (double)end.Y - start.Y;
        var lengthSquared = (directionX * directionX) +
            (directionY * directionY);
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
}
