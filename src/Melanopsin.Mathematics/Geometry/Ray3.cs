// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-dimensional ray <c>origin + t * direction</c> with <c>t &gt;= 0</c>.
/// </summary>
/// <remarks>
/// Direction is not required to be normalized. Consequently every intersection
/// distance parameter is expressed in units of the supplied direction vector.
/// Call <see cref="Vector3.Normalize"/> before constructing a ray when distance
/// in world units is required.
/// </remarks>
public readonly partial record struct Ray3 {
    public Ray3(Vector3 origin, Vector3 direction) {
        if (!origin.IsFinite || !direction.IsFinite) {
            throw new ArgumentException(
                "Ray origin and direction must be finite.",
                nameof(origin));
        }

        if (direction.X == 0 && direction.Y == 0 && direction.Z == 0) {
            throw new ArgumentException(
                "A ray direction must be non-zero.",
                nameof(direction));
        }

        Origin = origin;
        Direction = direction;
    }

    public Vector3 Origin { get; }

    public Vector3 Direction { get; }

    public Vector3 GetPoint(float distance) {
        return Origin + (Direction * distance);
    }

    public bool TryIntersect(
        Aabb3 bounds,
        out float nearDistance,
        out float farDistance) {
        return TryIntersect(
            bounds,
            out nearDistance,
            out farDistance,
            out _);
    }

    /// <summary>
    /// Attempts to intersect a closed bounding box and reports mathematical
    /// absence separately from numerical failure.
    /// </summary>
    public bool TryIntersect(
        Aabb3 bounds,
        out float nearDistance,
        out float farDistance,
        out GeometryOperationReport report) {
        var near = double.NegativeInfinity;
        var far = double.PositiveInfinity;
        if (!UpdateSlab(
            Origin.X,
            Direction.X,
            bounds.Minimum.X,
            bounds.Maximum.X,
            ref near,
            ref far) ||
            !UpdateSlab(
                Origin.Y,
                Direction.Y,
                bounds.Minimum.Y,
                bounds.Maximum.Y,
                ref near,
                ref far) ||
            !UpdateSlab(
                Origin.Z,
                Direction.Z,
                bounds.Minimum.Z,
                bounds.Maximum.Z,
                ref near,
                ref far)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoxIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        near = Math.Max(near, 0);
        if (!MatrixNumerics.IsRepresentableAsFloat(near) ||
            !MatrixNumerics.IsRepresentableAsFloat(far)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoxIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (near > far) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoxIntersection,
                far < 0 ? GeometryStatus.BehindOrigin : GeometryStatus.NoIntersection);
            return false;
        }

        nearDistance = (float)near;
        farDistance = (float)far;
        report = new GeometryOperationReport(
            GeometryOperation.RayBoxIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    public bool TryIntersect(Plane3 plane, out float distance) {
        return TryIntersect(plane, out distance, out _);
    }

    /// <summary>
    /// Attempts to intersect a plane and reports a parallel ray separately.
    /// </summary>
    public bool TryIntersect(
        Plane3 plane,
        out float distance,
        out GeometryOperationReport report) {
        var denominator = GeometryPredicates.Dot(plane.Normal, Direction);
        if (!double.IsFinite(denominator)) {
            distance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayPlaneIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (denominator == 0) {
            distance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayPlaneIntersection,
                GeometryStatus.Parallel);
            return false;
        }

        var distanceValue =
            -(GeometryPredicates.Dot(plane.Normal, Origin) + plane.Distance) /
            denominator;
        if (!MatrixNumerics.IsRepresentableAsFloat(distanceValue)) {
            distance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayPlaneIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (distanceValue < 0) {
            distance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayPlaneIntersection,
                GeometryStatus.BehindOrigin);
            return false;
        }

        distance = (float)distanceValue;
        report = new GeometryOperationReport(
            GeometryOperation.RayPlaneIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    public bool TryIntersect(Sphere3 sphere, out float nearDistance, out float farDistance) {
        return TryIntersect(sphere, out nearDistance, out farDistance, out _);
    }

    /// <summary>
    /// Attempts to intersect a solid sphere and reports a miss, a sphere
    /// entirely behind the ray, or numerical failure.
    /// </summary>
    public bool TryIntersect(
        Sphere3 sphere,
        out float nearDistance,
        out float farDistance,
        out GeometryOperationReport report) {
        var offsetX = (double)Origin.X - sphere.Center.X;
        var offsetY = (double)Origin.Y - sphere.Center.Y;
        var offsetZ = (double)Origin.Z - sphere.Center.Z;
        var a = ((double)Direction.X * Direction.X) +
            ((double)Direction.Y * Direction.Y) +
            ((double)Direction.Z * Direction.Z);
        var b = 2 * ((offsetX * Direction.X) +
            (offsetY * Direction.Y) +
            (offsetZ * Direction.Z));
        var c = (offsetX * offsetX) +
            (offsetY * offsetY) +
            (offsetZ * offsetZ) -
            ((double)sphere.Radius * sphere.Radius);
        var discriminant = (b * b) - (4 * a * c);
        if (!double.IsFinite(discriminant)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySphereIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (discriminant < 0) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySphereIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        var root = Math.Sqrt(discriminant);
        double first;
        double second;
        if (root == 0) {
            first = -b / (2 * a);
            second = first;
        } else {
            var q = -0.5 * (b + Math.CopySign(root, b));
            first = q / a;
            second = c / q;
            if (first > second) {
                (first, second) = (second, first);
            }
        }

        if (!double.IsFinite(first) || !double.IsFinite(second)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySphereIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (second < 0) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySphereIntersection,
                GeometryStatus.BehindOrigin);
            return false;
        }

        first = Math.Max(first, 0);
        if (!MatrixNumerics.IsRepresentableAsFloat(first) ||
            !MatrixNumerics.IsRepresentableAsFloat(second)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySphereIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        nearDistance = (float)first;
        farDistance = (float)second;
        report = new GeometryOperationReport(
            GeometryOperation.RaySphereIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Tests a ray-triangle intersection using the Möller-Trumbore algorithm.
    /// </summary>
    /// <remarks>
    /// Reference: Möller and Trumbore (1997),
    /// <see href="https://doi.org/10.1080/10867651.1997.10487468">doi:10.1080/10867651.1997.10487468</see>.
    /// A determinant of exactly zero is classified as parallel or degenerate;
    /// finite non-zero determinants are never rejected by an absolute cutoff.
    /// Callers needing approximate parallel classification must apply their own
    /// scale-relative tolerance.
    /// </remarks>
    public bool TryIntersect(
        Triangle3 triangle,
        out float distance,
        out Vector3 barycentric,
        out GeometryOperationReport report) {
        double firstX = triangle.First.X;
        double firstY = triangle.First.Y;
        double firstZ = triangle.First.Z;
        var edge1 = (
            X: (double)triangle.Second.X - firstX,
            Y: (double)triangle.Second.Y - firstY,
            Z: (double)triangle.Second.Z - firstZ);
        var edge2 = (
            X: (double)triangle.Third.X - firstX,
            Y: (double)triangle.Third.Y - firstY,
            Z: (double)triangle.Third.Z - firstZ);
        double directionX = Direction.X;
        double directionY = Direction.Y;
        double directionZ = Direction.Z;
        var cross = (
            X: (directionY * edge2.Z) - (directionZ * edge2.Y),
            Y: (directionZ * edge2.X) - (directionX * edge2.Z),
            Z: (directionX * edge2.Y) - (directionY * edge2.X));
        var determinant = GeometryPredicates.Dot(edge1, cross);
        if (!double.IsFinite(determinant)) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (determinant == 0) {
            distance = float.NaN;
            barycentric = default;
            var normal = GeometryPredicates.Cross(
                triangle.Second - triangle.First,
                triangle.Third - triangle.First);
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                normal.X == 0 && normal.Y == 0 && normal.Z == 0
                    ? GeometryStatus.DegenerateGeometry
                    : GeometryStatus.Parallel);
            return false;
        }

        var inverseDeterminant = 1 / determinant;
        var offset = (
            X: (double)Origin.X - firstX,
            Y: (double)Origin.Y - firstY,
            Z: (double)Origin.Z - firstZ);
        var secondWeight =
            GeometryPredicates.Dot(offset, cross) * inverseDeterminant;
        if (secondWeight < 0 || secondWeight > 1) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        var crossOffset = (
            X: (offset.Y * edge1.Z) - (offset.Z * edge1.Y),
            Y: (offset.Z * edge1.X) - (offset.X * edge1.Z),
            Z: (offset.X * edge1.Y) - (offset.Y * edge1.X));
        var thirdWeight =
            ((directionX * crossOffset.X) +
                (directionY * crossOffset.Y) +
                (directionZ * crossOffset.Z)) * inverseDeterminant;
        if (thirdWeight < 0 || secondWeight + thirdWeight > 1) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        var distanceValue =
            ((edge2.X * crossOffset.X) +
                (edge2.Y * crossOffset.Y) +
                (edge2.Z * crossOffset.Z)) * inverseDeterminant;
        var firstWeight = 1 - secondWeight - thirdWeight;
        if (!double.IsFinite(distanceValue) ||
            !MatrixNumerics.IsRepresentableAsFloat(firstWeight) ||
            !MatrixNumerics.IsRepresentableAsFloat(secondWeight) ||
            !MatrixNumerics.IsRepresentableAsFloat(thirdWeight)) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (distanceValue < 0) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.BehindOrigin);
            return false;
        }

        if (!MatrixNumerics.IsRepresentableAsFloat(distanceValue)) {
            distance = float.NaN;
            barycentric = default;
            report = new GeometryOperationReport(
                GeometryOperation.RayTriangleIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        distance = (float)distanceValue;
        barycentric = new Vector3(
            (float)firstWeight,
            (float)secondWeight,
            (float)thirdWeight);
        report = new GeometryOperationReport(
            GeometryOperation.RayTriangleIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    private static bool UpdateSlab(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref double near,
        ref double far) {
        if (direction == 0) {
            return origin >= minimum && origin <= maximum;
        }

        var first = ((double)minimum - origin) / direction;
        var second = ((double)maximum - origin) / direction;
        if (first > second) {
            (first, second) = (second, first);
        }

        near = Math.Max(near, first);
        far = Math.Min(far, second);
        return near <= far;
    }
}
