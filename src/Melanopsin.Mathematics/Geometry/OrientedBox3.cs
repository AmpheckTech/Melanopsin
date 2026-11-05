// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-dimensional box with a center, orthonormal orientation, and
/// half-extents along its local axes.
/// </summary>
/// <remarks>
/// <see cref="Orientation"/> uses the library's row-major, column-vector
/// convention. Its columns are the local X, Y, and Z axes in world space.
/// The constructor requires those columns to be orthonormal to within a small
/// multiple of single-precision machine epsilon; this validation is about
/// representation conditioning, not an approximate geometric equality test.
/// </remarks>
public readonly record struct OrientedBox3 {
    private const double OrthogonalityTolerance =
        32 * ScalarMath.MachineEpsilonSingle;

    public OrientedBox3(
        Vector3 center,
        Vector3 halfExtents,
        Matrix3x3 orientation) {
        if (!center.IsFinite) {
            throw new ArgumentException(
                "The center must be finite.",
                nameof(center));
        }

        if (!halfExtents.IsFinite) {
            throw new ArgumentException(
                "The half-extents must be finite.",
                nameof(halfExtents));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(halfExtents.X);
        ArgumentOutOfRangeException.ThrowIfNegative(halfExtents.Y);
        ArgumentOutOfRangeException.ThrowIfNegative(halfExtents.Z);
        ValidateOrientation(orientation);
        Center = center;
        HalfExtents = halfExtents;
        Orientation = orientation;
    }

    public Vector3 Center { get; }

    public Vector3 HalfExtents { get; }

    public Matrix3x3 Orientation { get; }

    public Vector3 AxisX => new(
        Orientation.M11,
        Orientation.M21,
        Orientation.M31);

    public Vector3 AxisY => new(
        Orientation.M12,
        Orientation.M22,
        Orientation.M32);

    public Vector3 AxisZ => new(
        Orientation.M13,
        Orientation.M23,
        Orientation.M33);

    /// <summary>
    /// Gets the volume, evaluated in double precision. The result is positive
    /// infinity when it exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Volume =>
        (float)(8d * HalfExtents.X * HalfExtents.Y * HalfExtents.Z);

    /// <summary>
    /// Gets the finite axis-aligned bounds. Coordinates are saturated at the
    /// finite single-precision limits if the exact bound lies outside the
    /// representable coordinate range.
    /// </summary>
    public Aabb3 Bounds {
        get {
            var extentX =
                (Math.Abs((double)Orientation.M11) * HalfExtents.X) +
                (Math.Abs((double)Orientation.M12) * HalfExtents.Y) +
                (Math.Abs((double)Orientation.M13) * HalfExtents.Z);
            var extentY =
                (Math.Abs((double)Orientation.M21) * HalfExtents.X) +
                (Math.Abs((double)Orientation.M22) * HalfExtents.Y) +
                (Math.Abs((double)Orientation.M23) * HalfExtents.Z);
            var extentZ =
                (Math.Abs((double)Orientation.M31) * HalfExtents.X) +
                (Math.Abs((double)Orientation.M32) * HalfExtents.Y) +
                (Math.Abs((double)Orientation.M33) * HalfExtents.Z);
            return new Aabb3(
                new Vector3(
                    FloatingPointMath.SaturatingAdd(Center.X, -extentX),
                    FloatingPointMath.SaturatingAdd(Center.Y, -extentY),
                    FloatingPointMath.SaturatingAdd(Center.Z, -extentZ)),
                new Vector3(
                    FloatingPointMath.SaturatingAdd(Center.X, extentX),
                    FloatingPointMath.SaturatingAdd(Center.Y, extentY),
                    FloatingPointMath.SaturatingAdd(Center.Z, extentZ)));
        }
    }

    public static OrientedBox3 FromBounds(Aabb3 bounds) {
        return new OrientedBox3(
            bounds.Center,
            bounds.Extents,
            Matrix3x3.Identity);
    }

    public bool Contains(Vector3 point) {
        var offsetX = (double)point.X - Center.X;
        var offsetY = (double)point.Y - Center.Y;
        var offsetZ = (double)point.Z - Center.Z;
        return Math.Abs(Dot(offsetX, offsetY, offsetZ, AxisX)) <= HalfExtents.X &&
            Math.Abs(Dot(offsetX, offsetY, offsetZ, AxisY)) <= HalfExtents.Y &&
            Math.Abs(Dot(offsetX, offsetY, offsetZ, AxisZ)) <= HalfExtents.Z;
    }

    public Vector3 ClosestPoint(Vector3 point) {
        double offsetX = (double)point.X - Center.X;
        double offsetY = (double)point.Y - Center.Y;
        double offsetZ = (double)point.Z - Center.Z;
        var localX = ScalarMath.Clamp(
            Dot(offsetX, offsetY, offsetZ, AxisX),
            -HalfExtents.X,
            HalfExtents.X);
        var localY = ScalarMath.Clamp(
            Dot(offsetX, offsetY, offsetZ, AxisY),
            -HalfExtents.Y,
            HalfExtents.Y);
        var localZ = ScalarMath.Clamp(
            Dot(offsetX, offsetY, offsetZ, AxisZ),
            -HalfExtents.Z,
            HalfExtents.Z);
        return new Vector3(
            (float)((double)Center.X +
                (AxisX.X * localX) +
                (AxisY.X * localY) +
                (AxisZ.X * localZ)),
            (float)((double)Center.Y +
                (AxisX.Y * localX) +
                (AxisY.Y * localY) +
                (AxisZ.Y * localZ)),
            (float)((double)Center.Z +
                (AxisX.Z * localX) +
                (AxisY.Z * localY) +
                (AxisZ.Z * localZ)));
    }

    public float DistanceSquared(Vector3 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector3 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    public bool Intersects(Aabb3 bounds) {
        return Intersects(FromBounds(bounds));
    }

    /// <summary>
    /// Tests overlap with another oriented box using the separating-axis
    /// theorem.
    /// </summary>
    /// <remarks>
    /// Touching boxes count as intersecting. The test is exact at the level of
    /// the supplied finite-precision values; it does not classify boxes as
    /// separated merely because their overlap is small.
    /// </remarks>
    public bool Intersects(OrientedBox3 other) {
        double offsetX = (double)other.Center.X - Center.X;
        double offsetY = (double)other.Center.Y - Center.Y;
        double offsetZ = (double)other.Center.Z - Center.Z;
        if (SeparatedAlongFaceAxes(this, other, offsetX, offsetY, offsetZ) ||
            SeparatedAlongFaceAxes(other, this, -offsetX, -offsetY, -offsetZ)) {
            return false;
        }

        for (var firstIndex = 0; firstIndex < 3; firstIndex++) {
            var firstAxis = GetAxis(this, firstIndex);
            for (var secondIndex = 0; secondIndex < 3; secondIndex++) {
                var secondAxis = GetAxis(other, secondIndex);
                var crossX = ((double)firstAxis.Y * secondAxis.Z) -
                    ((double)firstAxis.Z * secondAxis.Y);
                var crossY = ((double)firstAxis.Z * secondAxis.X) -
                    ((double)firstAxis.X * secondAxis.Z);
                var crossZ = ((double)firstAxis.X * secondAxis.Y) -
                    ((double)firstAxis.Y * secondAxis.X);
                var lengthSquared = (crossX * crossX) +
                    (crossY * crossY) +
                    (crossZ * crossZ);
                if (lengthSquared == 0) {
                    continue;
                }

                var inverseLength = 1 / Math.Sqrt(lengthSquared);
                var axisX = crossX * inverseLength;
                var axisY = crossY * inverseLength;
                var axisZ = crossZ * inverseLength;
                var distance = Math.Abs(
                    (offsetX * axisX) +
                    (offsetY * axisY) +
                    (offsetZ * axisZ));
                var firstRadius = ProjectedRadius(this, axisX, axisY, axisZ);
                var secondRadius = ProjectedRadius(other, axisX, axisY, axisZ);
                if (distance > firstRadius + secondRadius) {
                    return false;
                }
            }
        }

        return true;
    }

    public bool Intersects(Sphere3 sphere) {
        return GeometryPredicates.DistanceSquared(sphere.Center, ClosestPoint(sphere.Center)) <=
            ((double)sphere.Radius * sphere.Radius);
    }

    private static bool SeparatedAlongFaceAxes(
        OrientedBox3 first,
        OrientedBox3 second,
        double offsetX,
        double offsetY,
        double offsetZ) {
        for (var index = 0; index < 3; index++) {
            var axis = GetAxis(first, index);
            var distance = Math.Abs(Dot(offsetX, offsetY, offsetZ, axis));
            var radius = ProjectedRadius(first, axis.X, axis.Y, axis.Z) +
                ProjectedRadius(second, axis.X, axis.Y, axis.Z);
            if (distance > radius) {
                return true;
            }
        }

        return false;
    }

    private static double ProjectedRadius(
        OrientedBox3 box,
        double axisX,
        double axisY,
        double axisZ) {
        return
            (GetHalfExtent(box, 0) * Math.Abs(Dot(axisX, axisY, axisZ, GetAxis(box, 0)))) +
            (GetHalfExtent(box, 1) * Math.Abs(Dot(axisX, axisY, axisZ, GetAxis(box, 1)))) +
            (GetHalfExtent(box, 2) * Math.Abs(Dot(axisX, axisY, axisZ, GetAxis(box, 2))));
    }

    private static double Dot(
        double x,
        double y,
        double z,
        Vector3 axis) {
        return (x * axis.X) + (y * axis.Y) + (z * axis.Z);
    }

    private static Vector3 GetAxis(OrientedBox3 box, int index) {
        return index switch {
            0 => box.AxisX,
            1 => box.AxisY,
            _ => box.AxisZ,
        };
    }

    private static float GetHalfExtent(OrientedBox3 box, int index) {
        return index switch {
            0 => box.HalfExtents.X,
            1 => box.HalfExtents.Y,
            _ => box.HalfExtents.Z,
        };
    }

    private static void ValidateOrientation(Matrix3x3 orientation) {
        if (!float.IsFinite(orientation.M11) ||
            !float.IsFinite(orientation.M12) ||
            !float.IsFinite(orientation.M13) ||
            !float.IsFinite(orientation.M21) ||
            !float.IsFinite(orientation.M22) ||
            !float.IsFinite(orientation.M23) ||
            !float.IsFinite(orientation.M31) ||
            !float.IsFinite(orientation.M32) ||
            !float.IsFinite(orientation.M33)) {
            throw new ArgumentException(
                "The orientation must contain only finite values.",
                nameof(orientation));
        }

        Span<Vector3> axes = stackalloc Vector3[3];
        axes[0] = new Vector3(
            orientation.M11,
            orientation.M21,
            orientation.M31);
        axes[1] = new Vector3(
            orientation.M12,
            orientation.M22,
            orientation.M32);
        axes[2] = new Vector3(
            orientation.M13,
            orientation.M23,
            orientation.M33);
        for (var index = 0; index < axes.Length; index++) {
            if (Math.Abs(GeometryPredicates.Length(axes[index]) - 1) >
                OrthogonalityTolerance) {
                throw new ArgumentException(
                    "Orientation axes must be unit length.",
                    nameof(orientation));
            }
        }

        if (Math.Abs(GeometryPredicates.Dot(axes[0], axes[1])) > OrthogonalityTolerance ||
            Math.Abs(GeometryPredicates.Dot(axes[0], axes[2])) > OrthogonalityTolerance ||
            Math.Abs(GeometryPredicates.Dot(axes[1], axes[2])) > OrthogonalityTolerance) {
            throw new ArgumentException(
                "Orientation axes must be mutually orthogonal.",
                nameof(orientation));
        }
    }
}
