// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A triangle in three-dimensional space.
/// </summary>
/// <remarks>
/// A degenerate triangle is representable, but its normal, area, and
/// barycentric coordinates are undefined. Methods that require an orientation
/// return <see langword="false"/> for degenerate input rather than applying an
/// absolute area threshold.
/// </remarks>
public readonly partial record struct Triangle3 {
    public Triangle3(Vector3 first, Vector3 second, Vector3 third) {
        if (!first.IsFinite || !second.IsFinite || !third.IsFinite) {
            throw new ArgumentException(
                "Triangle vertices must be finite.",
                nameof(first));
        }

        First = first;
        Second = second;
        Third = third;
    }

    public Vector3 First { get; }

    public Vector3 Second { get; }

    public Vector3 Third { get; }

    public Vector3 Centroid => new(
        (float)(((double)First.X + Second.X + Third.X) / 3),
        (float)(((double)First.Y + Second.Y + Third.Y) / 3),
        (float)(((double)First.Z + Second.Z + Third.Z) / 3));

    /// <summary>
    /// Gets the unnormalized cross product of the first two edges. Edge
    /// subtraction and products are evaluated in double precision. A component
    /// is infinite when its mathematical value exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public Vector3 Normal {
        get {
            var cross = CrossEdges();
            return new Vector3(
                (float)cross.X,
                (float)cross.Y,
                (float)cross.Z);
        }
    }

    /// <summary>
    /// Gets the area. The result is positive infinity when the mathematical
    /// area exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Area {
        get {
            var cross = CrossEdges();
            var length = Math.Sqrt(
                (cross.X * cross.X) +
                (cross.Y * cross.Y) +
                (cross.Z * cross.Z));
            return (float)(0.5 * length);
        }
    }

    public bool TryGetNormal(out Vector3 normal) {
        return TryGetNormal(out normal, out _);
    }

    private (double X, double Y, double Z) CrossEdges() {
        var firstX = (double)Second.X - First.X;
        var firstY = (double)Second.Y - First.Y;
        var firstZ = (double)Second.Z - First.Z;
        var secondX = (double)Third.X - First.X;
        var secondY = (double)Third.Y - First.Y;
        var secondZ = (double)Third.Z - First.Z;
        return (
            (firstY * secondZ) - (firstZ * secondY),
            (firstZ * secondX) - (firstX * secondZ),
            (firstX * secondY) - (firstY * secondX));
    }

    /// <summary>
    /// Attempts to construct a unit normal and reports degenerate geometry or
    /// non-finite arithmetic.
    /// </summary>
    public bool TryGetNormal(
        out Vector3 normal,
        out GeometryOperationReport report) {
        var firstX = (double)Second.X - First.X;
        var firstY = (double)Second.Y - First.Y;
        var firstZ = (double)Second.Z - First.Z;
        var secondX = (double)Third.X - First.X;
        var secondY = (double)Third.Y - First.Y;
        var secondZ = (double)Third.Z - First.Z;
        var crossX = (firstY * secondZ) - (firstZ * secondY);
        var crossY = (firstZ * secondX) - (firstX * secondZ);
        var crossZ = (firstX * secondY) - (firstY * secondX);
        if (!double.IsFinite(crossX) ||
            !double.IsFinite(crossY) ||
            !double.IsFinite(crossZ)) {
            normal = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleNormal,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        var length = Math.Sqrt(
            (crossX * crossX) + (crossY * crossY) + (crossZ * crossZ));
        if (length == 0) {
            normal = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleNormal,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        normal = new Vector3(
            (float)(crossX / length),
            (float)(crossY / length),
            (float)(crossZ / length));

        report = new GeometryOperationReport(
            GeometryOperation.TriangleNormal,
            GeometryStatus.Succeeded);
        return true;
    }

    public bool TryGetBarycentric(
        Vector3 point,
        out Vector3 barycentric) {
        return TryGetBarycentric(point, out barycentric, out _);
    }

    /// <summary>
    /// Attempts to evaluate barycentric coordinates and reports non-finite
    /// input, degenerate geometry, or numerical failure.
    /// </summary>
    public bool TryGetBarycentric(
        Vector3 point,
        out Vector3 barycentric,
        out GeometryOperationReport report) {
        if (!point.IsFinite) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        var firstX = (double)Second.X - First.X;
        var firstY = (double)Second.Y - First.Y;
        var firstZ = (double)Second.Z - First.Z;
        var secondX = (double)Third.X - First.X;
        var secondY = (double)Third.Y - First.Y;
        var secondZ = (double)Third.Z - First.Z;
        var offsetX = (double)point.X - First.X;
        var offsetY = (double)point.Y - First.Y;
        var offsetZ = (double)point.Z - First.Z;
        var firstDot = (firstX * firstX) +
            (firstY * firstY) +
            (firstZ * firstZ);
        var crossDot = (firstX * secondX) +
            (firstY * secondY) +
            (firstZ * secondZ);
        var secondDot = (secondX * secondX) +
            (secondY * secondY) +
            (secondZ * secondZ);
        var determinant = (firstDot * secondDot) - (crossDot * crossDot);
        if (!double.IsFinite(determinant)) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (determinant == 0) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var offsetFirst = (offsetX * firstX) +
            (offsetY * firstY) +
            (offsetZ * firstZ);
        var offsetSecond = (offsetX * secondX) +
            (offsetY * secondY) +
            (offsetZ * secondZ);
        var secondWeight =
            ((secondDot * offsetFirst) - (crossDot * offsetSecond)) / determinant;
        var thirdWeight =
            ((firstDot * offsetSecond) - (crossDot * offsetFirst)) / determinant;
        var firstWeight = 1 - secondWeight - thirdWeight;
        if (!MatrixNumerics.IsRepresentableAsFloat(firstWeight) ||
            !MatrixNumerics.IsRepresentableAsFloat(secondWeight) ||
            !MatrixNumerics.IsRepresentableAsFloat(thirdWeight)) {
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.TriangleBarycentricCoordinates,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        barycentric = new Vector3(
            (float)firstWeight,
            (float)secondWeight,
            (float)thirdWeight);

        report = new GeometryOperationReport(
            GeometryOperation.TriangleBarycentricCoordinates,
            GeometryStatus.Succeeded);
        return true;
    }

    public Vector3 Barycentric(Vector3 point) {
        if (!TryGetBarycentric(point, out var result)) {
            throw new ArithmeticException(
                "Barycentric coordinates are undefined for a degenerate triangle.");
        }

        return result;
    }

    /// <summary>
    /// Tests containment in the closed triangle using an explicit barycentric
    /// tolerance. The default is zero; callers choose any approximation budget.
    /// </summary>
    public bool Contains(Vector3 point, float tolerance = 0) {
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

    public Vector3 ClosestPoint(Vector3 point) {
        double firstX = First.X;
        double firstY = First.Y;
        double firstZ = First.Z;
        double abX = (double)Second.X - firstX;
        double abY = (double)Second.Y - firstY;
        double abZ = (double)Second.Z - firstZ;
        double acX = (double)Third.X - firstX;
        double acY = (double)Third.Y - firstY;
        double acZ = (double)Third.Z - firstZ;
        double apX = (double)point.X - firstX;
        double apY = (double)point.Y - firstY;
        double apZ = (double)point.Z - firstZ;
        var firstDot = (abX * apX) + (abY * apY) + (abZ * apZ);
        var secondDot = (acX * apX) + (acY * apY) + (acZ * apZ);
        if (firstDot <= 0 && secondDot <= 0) {
            return First;
        }

        double bpX = (double)point.X - Second.X;
        double bpY = (double)point.Y - Second.Y;
        double bpZ = (double)point.Z - Second.Z;
        var thirdDot = (abX * bpX) + (abY * bpY) + (abZ * bpZ);
        var fourthDot = (acX * bpX) + (acY * bpY) + (acZ * bpZ);
        if (thirdDot >= 0 && fourthDot <= thirdDot) {
            return Second;
        }

        var firstEdgeRegion = (firstDot * fourthDot) - (thirdDot * secondDot);
        if (firstEdgeRegion <= 0 && firstDot >= 0 && thirdDot <= 0) {
            var amount = firstDot / (firstDot - thirdDot);
            return new Vector3(
                (float)(firstX + (amount * abX)),
                (float)(firstY + (amount * abY)),
                (float)(firstZ + (amount * abZ)));
        }

        double cpX = (double)point.X - Third.X;
        double cpY = (double)point.Y - Third.Y;
        double cpZ = (double)point.Z - Third.Z;
        var fifthDot = (abX * cpX) + (abY * cpY) + (abZ * cpZ);
        var sixthDot = (acX * cpX) + (acY * cpY) + (acZ * cpZ);
        if (sixthDot >= 0 && fifthDot <= sixthDot) {
            return Third;
        }

        var secondEdgeRegion = (fifthDot * secondDot) - (firstDot * sixthDot);
        if (secondEdgeRegion <= 0 && secondDot >= 0 && sixthDot <= 0) {
            var amount = secondDot / (secondDot - sixthDot);
            return new Vector3(
                (float)(firstX + (amount * acX)),
                (float)(firstY + (amount * acY)),
                (float)(firstZ + (amount * acZ)));
        }

        var thirdEdgeRegion = (thirdDot * sixthDot) - (fifthDot * fourthDot);
        if (thirdEdgeRegion <= 0 &&
            fourthDot - thirdDot >= 0 &&
            fifthDot - sixthDot >= 0) {
            var amount = (fourthDot - thirdDot) /
                ((fourthDot - thirdDot) + (fifthDot - sixthDot));
            return new Vector3(
                (float)((double)Second.X + (amount * ((double)Third.X - Second.X))),
                (float)((double)Second.Y + (amount * ((double)Third.Y - Second.Y))),
                (float)((double)Second.Z + (amount * ((double)Third.Z - Second.Z))));
        }

        var denominator = firstEdgeRegion + secondEdgeRegion + thirdEdgeRegion;
        if (denominator == 0) {
            return ClosestPointOnDegenerateTriangle(point);
        }

        var secondWeight = secondEdgeRegion / denominator;
        var thirdWeight = firstEdgeRegion / denominator;
        return new Vector3(
            (float)(firstX + (secondWeight * abX) + (thirdWeight * acX)),
            (float)(firstY + (secondWeight * abY) + (thirdWeight * acY)),
            (float)(firstZ + (secondWeight * abZ) + (thirdWeight * acZ)));
    }

    private Vector3 ClosestPointOnDegenerateTriangle(Vector3 point) {
        var firstCandidate = ClosestPointOnSegment(point, First, Second);
        var secondCandidate = ClosestPointOnSegment(point, Second, Third);
        var thirdCandidate = ClosestPointOnSegment(point, Third, First);
        var firstDistance = DistanceSquared(point, firstCandidate);
        var secondDistance = DistanceSquared(point, secondCandidate);
        var thirdDistance = DistanceSquared(point, thirdCandidate);
        if (firstDistance <= secondDistance && firstDistance <= thirdDistance) {
            return firstCandidate;
        }

        return secondDistance <= thirdDistance ? secondCandidate : thirdCandidate;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end) {
        double directionX = (double)end.X - start.X;
        double directionY = (double)end.Y - start.Y;
        double directionZ = (double)end.Z - start.Z;
        var lengthSquared = (directionX * directionX) +
            (directionY * directionY) +
            (directionZ * directionZ);
        if (lengthSquared == 0) {
            return start;
        }

        var amount = ScalarMath.Clamp(
            ((((double)point.X - start.X) * directionX) +
                (((double)point.Y - start.Y) * directionY) +
                (((double)point.Z - start.Z) * directionZ)) / lengthSquared,
            0,
            1);
        return new Vector3(
            (float)(start.X + (amount * directionX)),
            (float)(start.Y + (amount * directionY)),
            (float)(start.Z + (amount * directionZ)));
    }

    private static double DistanceSquared(Vector3 first, Vector3 second) {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return (x * x) + (y * y) + (z * z);
    }
}
