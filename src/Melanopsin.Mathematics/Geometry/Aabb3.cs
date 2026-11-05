// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An axis-aligned bounding box in three-dimensional space.
/// </summary>
/// <remarks>
/// Bounds are inclusive and componentwise ordered. Empty boxes are not
/// represented; use a nullable <see cref="Aabb3"/> at call sites that need an
/// absence value. Transformation of a box is always conservative: the result
/// is the axis-aligned hull of the eight transformed corners.
/// </remarks>
public readonly partial record struct Aabb3 {
    public Aabb3(Vector3 minimum, Vector3 maximum) {
        if (!minimum.IsFinite || !maximum.IsFinite) {
            throw new ArgumentException(
                "Bounding-box bounds must be finite.",
                nameof(minimum));
        }

        if (minimum.X > maximum.X ||
            minimum.Y > maximum.Y ||
            minimum.Z > maximum.Z) {
            throw new ArgumentException(
                "Each minimum bound must not exceed the corresponding maximum bound.",
                nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public Vector3 Minimum { get; }

    public Vector3 Maximum { get; }

    /// <summary>
    /// Gets the midpoint. Coordinates are evaluated in double precision so
    /// opposite finite bounds cannot overflow before the scale by one half.
    /// </summary>
    public Vector3 Center => new(
        (float)(((double)Minimum.X + Maximum.X) * 0.5),
        (float)(((double)Minimum.Y + Maximum.Y) * 0.5),
        (float)(((double)Minimum.Z + Maximum.Z) * 0.5));

    /// <summary>
    /// Gets the componentwise extent. A component is positive infinity when
    /// the mathematical extent exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public Vector3 Size => new(
        (float)((double)Maximum.X - Minimum.X),
        (float)((double)Maximum.Y - Minimum.Y),
        (float)((double)Maximum.Z - Minimum.Z));

    /// <summary>
    /// Gets the componentwise half-size. The calculation is representable for
    /// every valid pair of finite single-precision bounds.
    /// </summary>
    public Vector3 Extents => new(
        (float)(((double)Maximum.X - Minimum.X) * 0.5),
        (float)(((double)Maximum.Y - Minimum.Y) * 0.5),
        (float)(((double)Maximum.Z - Minimum.Z) * 0.5));

    /// <summary>
    /// Gets the volume, evaluated in double precision. The result is positive
    /// infinity when the mathematical volume exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Volume => (float)(
        ((double)Maximum.X - Minimum.X) *
        ((double)Maximum.Y - Minimum.Y) *
        ((double)Maximum.Z - Minimum.Z));

    /// <summary>
    /// Gets the surface area, evaluated in double precision. The result is
    /// positive infinity when it exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float SurfaceArea {
        get {
            var x = (double)Maximum.X - Minimum.X;
            var y = (double)Maximum.Y - Minimum.Y;
            var z = (double)Maximum.Z - Minimum.Z;
            return (float)(2 * ((x * y) + (y * z) + (z * x)));
        }
    }

    public static Aabb3 FromCenterExtents(Vector3 center, Vector3 extents) {
        if (!center.IsFinite || !extents.IsFinite) {
            throw new ArgumentException(
                "Center and extents must be finite.",
                nameof(center));
        }

        if (extents.X < 0 || extents.Y < 0 || extents.Z < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(extents),
                "Extents must be non-negative.");
        }

        return new Aabb3(
            new Vector3(
                FloatingPointMath.SaturatingAdd(center.X, -extents.X),
                FloatingPointMath.SaturatingAdd(center.Y, -extents.Y),
                FloatingPointMath.SaturatingAdd(center.Z, -extents.Z)),
            new Vector3(
                FloatingPointMath.SaturatingAdd(center.X, extents.X),
                FloatingPointMath.SaturatingAdd(center.Y, extents.Y),
                FloatingPointMath.SaturatingAdd(center.Z, extents.Z)));
    }

    public static Aabb3 FromPoints(ReadOnlySpan<Vector3> points) {
        if (points.IsEmpty) {
            throw new ArgumentException(
                "At least one point is required.",
                nameof(points));
        }

        var minimum = points[0];
        var maximum = points[0];
        if (!minimum.IsFinite) {
            throw new ArgumentException(
                "Points must be finite.",
                nameof(points));
        }

        for (var index = 1; index < points.Length; index++) {
            var point = points[index];
            if (!point.IsFinite) {
                throw new ArgumentException(
                    "Points must be finite.",
                    nameof(points));
            }

            minimum = new Vector3(
                MathF.Min(minimum.X, point.X),
                MathF.Min(minimum.Y, point.Y),
                MathF.Min(minimum.Z, point.Z));
            maximum = new Vector3(
                MathF.Max(maximum.X, point.X),
                MathF.Max(maximum.Y, point.Y),
                MathF.Max(maximum.Z, point.Z));
        }

        return new Aabb3(minimum, maximum);
    }

    public bool Contains(Vector3 point) {
        return point.X >= Minimum.X && point.X <= Maximum.X &&
            point.Y >= Minimum.Y && point.Y <= Maximum.Y &&
            point.Z >= Minimum.Z && point.Z <= Maximum.Z;
    }

    public bool Contains(Aabb3 other) {
        return other.Minimum.X >= Minimum.X &&
            other.Minimum.Y >= Minimum.Y &&
            other.Minimum.Z >= Minimum.Z &&
            other.Maximum.X <= Maximum.X &&
            other.Maximum.Y <= Maximum.Y &&
            other.Maximum.Z <= Maximum.Z;
    }

    public bool Intersects(Aabb3 other) {
        return Minimum.X <= other.Maximum.X &&
            Maximum.X >= other.Minimum.X &&
            Minimum.Y <= other.Maximum.Y &&
            Maximum.Y >= other.Minimum.Y &&
            Minimum.Z <= other.Maximum.Z &&
            Maximum.Z >= other.Minimum.Z;
    }

    public Aabb3 Union(Aabb3 other) {
        return new Aabb3(
            new Vector3(
                MathF.Min(Minimum.X, other.Minimum.X),
                MathF.Min(Minimum.Y, other.Minimum.Y),
                MathF.Min(Minimum.Z, other.Minimum.Z)),
            new Vector3(
                MathF.Max(Maximum.X, other.Maximum.X),
                MathF.Max(Maximum.Y, other.Maximum.Y),
                MathF.Max(Maximum.Z, other.Maximum.Z)));
    }

    public bool TryIntersect(Aabb3 other, out Aabb3 intersection) {
        return TryIntersect(other, out intersection, out _);
    }

    /// <summary>
    /// Attempts to intersect two bounding boxes and reports whether a result
    /// exists.
    /// </summary>
    public bool TryIntersect(
        Aabb3 other,
        out Aabb3 intersection,
        out GeometryOperationReport report) {
        if (!Intersects(other)) {
            intersection = default;
            report = new GeometryOperationReport(
                GeometryOperation.BoundsIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        intersection = new Aabb3(
            new Vector3(
                MathF.Max(Minimum.X, other.Minimum.X),
                MathF.Max(Minimum.Y, other.Minimum.Y),
                MathF.Max(Minimum.Z, other.Minimum.Z)),
            new Vector3(
                MathF.Min(Maximum.X, other.Maximum.X),
                MathF.Min(Maximum.Y, other.Maximum.Y),
                MathF.Min(Maximum.Z, other.Maximum.Z)));
        report = new GeometryOperationReport(
            GeometryOperation.BoundsIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    public Aabb3 Intersect(Aabb3 other) {
        if (!TryIntersect(other, out var intersection)) {
            throw new ArithmeticException(
                "The bounding boxes do not intersect.");
        }

        return intersection;
    }

    public Aabb3 Inflate(Vector3 amount) {
        if (!amount.IsFinite ||
            amount.X < 0 ||
            amount.Y < 0 ||
            amount.Z < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Inflation amounts must be finite and non-negative.");
        }

        return new Aabb3(
            new Vector3(
                FloatingPointMath.SaturatingAdd(Minimum.X, -amount.X),
                FloatingPointMath.SaturatingAdd(Minimum.Y, -amount.Y),
                FloatingPointMath.SaturatingAdd(Minimum.Z, -amount.Z)),
            new Vector3(
                FloatingPointMath.SaturatingAdd(Maximum.X, amount.X),
                FloatingPointMath.SaturatingAdd(Maximum.Y, amount.Y),
                FloatingPointMath.SaturatingAdd(Maximum.Z, amount.Z)));
    }

    public Aabb3 Inflate(float amount) {
        return Inflate(new Vector3(amount, amount, amount));
    }

    public Vector3 ClosestPoint(Vector3 point) {
        return new Vector3(
            ScalarMath.Clamp(point.X, Minimum.X, Maximum.X),
            ScalarMath.Clamp(point.Y, Minimum.Y, Maximum.Y),
            ScalarMath.Clamp(point.Z, Minimum.Z, Maximum.Z));
    }

    /// <summary>
    /// Gets the squared distance to the closed box. The value is positive
    /// infinity when its mathematical value exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float DistanceSquared(Vector3 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector3 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    /// <summary>
    /// Computes the axis-aligned hull of the transformed corners.
    /// </summary>
    public Aabb3 Transform(Matrix4x4 transform) {
        if (!TryTransform(transform, out var result)) {
            throw new ArithmeticException(
                "A transformed corner is not representable as a finite point.");
        }

        return result;
    }

    public bool TryTransform(Matrix4x4 transform, out Aabb3 result) {
        return TryTransform(transform, out result, out _);
    }

    /// <summary>
    /// Attempts a conservative box transformation and reports a projective
    /// singularity or non-representable result.
    /// </summary>
    /// <remarks>
    /// The result is the axis-aligned hull of the eight transformed corners.
    /// Projective transformation is supported only when every transformed
    /// corner has a finite Cartesian representative and all non-zero
    /// homogeneous W values have the same sign. If the box touches or crosses
    /// the plane at infinity, a finite corner hull is not a sound conservative
    /// bound; that case returns
    /// <see cref="GeometryStatus.ProjectiveSingularity"/>.
    /// </remarks>
    public bool TryTransform(
        Matrix4x4 transform,
        out Aabb3 result,
        out GeometryOperationReport report) {
        if (!transform.IsFinite) {
            result = default;
            report = new GeometryOperationReport(
                GeometryOperation.BoundsTransform,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(Minimum.X, Minimum.Y, Minimum.Z);
        corners[1] = new Vector3(Maximum.X, Minimum.Y, Minimum.Z);
        corners[2] = new Vector3(Minimum.X, Maximum.Y, Minimum.Z);
        corners[3] = new Vector3(Maximum.X, Maximum.Y, Minimum.Z);
        corners[4] = new Vector3(Minimum.X, Minimum.Y, Maximum.Z);
        corners[5] = new Vector3(Maximum.X, Minimum.Y, Maximum.Z);
        corners[6] = new Vector3(Minimum.X, Maximum.Y, Maximum.Z);
        corners[7] = new Vector3(Maximum.X, Maximum.Y, Maximum.Z);
        var hasPositiveW = false;
        var hasNegativeW = false;
        for (var index = 0; index < corners.Length; index++) {
            var corner = corners[index];
            var w =
                ((double)transform.M41 * corner.X) +
                ((double)transform.M42 * corner.Y) +
                ((double)transform.M43 * corner.Z) +
                transform.M44;
            if (w == 0) {
                result = default;
                report = new GeometryOperationReport(
                    GeometryOperation.BoundsTransform,
                    GeometryStatus.ProjectiveSingularity);
                return false;
            }

            if (!double.IsFinite(w)) {
                result = default;
                report = new GeometryOperationReport(
                    GeometryOperation.BoundsTransform,
                    GeometryStatus.NonFiniteOutput);
                return false;
            }

            hasPositiveW |= w > 0;
            hasNegativeW |= w < 0;
        }

        if (hasPositiveW && hasNegativeW) {
            result = default;
            report = new GeometryOperationReport(
                GeometryOperation.BoundsTransform,
                GeometryStatus.ProjectiveSingularity);
            return false;
        }

        var minimum = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);
        var maximum = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity);
        for (var index = 0; index < corners.Length; index++) {
            if (!transform.TryTransformPoint(
                    corners[index],
                    out var point,
                    out var transformReport)) {
                result = default;
                report = new GeometryOperationReport(
                    GeometryOperation.BoundsTransform,
                    MapTransformStatus(transformReport.Status));
                return false;
            }

            minimum = new Vector3(
                MathF.Min(minimum.X, point.X),
                MathF.Min(minimum.Y, point.Y),
                MathF.Min(minimum.Z, point.Z));
            maximum = new Vector3(
                MathF.Max(maximum.X, point.X),
                MathF.Max(maximum.Y, point.Y),
                MathF.Max(maximum.Z, point.Z));
        }

        result = new Aabb3(minimum, maximum);
        report = new GeometryOperationReport(
            GeometryOperation.BoundsTransform,
            GeometryStatus.Succeeded);
        return true;
    }

    private static GeometryStatus MapTransformStatus(
        MatrixTransformStatus status) {
        return status switch {
            MatrixTransformStatus.ProjectiveSingularity =>
                GeometryStatus.ProjectiveSingularity,
            MatrixTransformStatus.NonFiniteInput or
                MatrixTransformStatus.NonFiniteMatrix =>
                GeometryStatus.NonFiniteInput,
            MatrixTransformStatus.NonFiniteOutput or
                MatrixTransformStatus.NonFiniteW =>
                GeometryStatus.NonFiniteOutput,
            MatrixTransformStatus.DegenerateDirection =>
                GeometryStatus.DegenerateGeometry,
            MatrixTransformStatus.NotAffine =>
                GeometryStatus.NumericalFailure,
            MatrixTransformStatus.Succeeded => GeometryStatus.Succeeded,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown matrix transform status."),
        };
    }
}
