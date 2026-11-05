namespace Melanopsin.Mathematics;

/// <summary>
/// An axis-aligned bounding box in two-dimensional space.
/// </summary>
/// <remarks>
/// Bounds are inclusive and componentwise ordered. Empty boxes are not
/// represented; use a nullable <see cref="Aabb2"/> at call sites that need an
/// absence value.
/// </remarks>
public readonly partial record struct Aabb2 {
    public Aabb2(Vector2 minimum, Vector2 maximum) {
        if (!minimum.IsFinite || !maximum.IsFinite) {
            throw new ArgumentException(
                "Bounding-box bounds must be finite.",
                nameof(minimum));
        }

        if (minimum.X > maximum.X || minimum.Y > maximum.Y) {
            throw new ArgumentException(
                "Each minimum bound must not exceed the corresponding maximum bound.",
                nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public Vector2 Minimum { get; }

    public Vector2 Maximum { get; }

    /// <summary>
    /// Gets the midpoint. Coordinates are evaluated in double precision so
    /// opposite finite bounds cannot overflow before the scale by one half.
    /// </summary>
    public Vector2 Center => new(
        (float)(((double)Minimum.X + Maximum.X) * 0.5),
        (float)(((double)Minimum.Y + Maximum.Y) * 0.5));

    /// <summary>
    /// Gets the componentwise extent. A component is positive infinity when
    /// the mathematical extent exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public Vector2 Size => new(
        (float)((double)Maximum.X - Minimum.X),
        (float)((double)Maximum.Y - Minimum.Y));

    /// <summary>
    /// Gets the area, evaluated in double precision. The result is positive
    /// infinity when the mathematical area exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Area => (float)(
        ((double)Maximum.X - Minimum.X) *
        ((double)Maximum.Y - Minimum.Y));

    public static Aabb2 FromPoints(ReadOnlySpan<Vector2> points) {
        if (points.IsEmpty) {
            throw new ArgumentException(
                "At least one point is required.",
                nameof(points));
        }

        var minimum = points[0];
        var maximum = points[0];
        for (var index = 0; index < points.Length; index++) {
            var point = points[index];
            if (!point.IsFinite) {
                throw new ArgumentException(
                    "The points must contain only finite values.",
                    nameof(points));
            }

            minimum = new Vector2(
                MathF.Min(minimum.X, point.X),
                MathF.Min(minimum.Y, point.Y));
            maximum = new Vector2(
                MathF.Max(maximum.X, point.X),
                MathF.Max(maximum.Y, point.Y));
        }

        return new Aabb2(minimum, maximum);
    }

    public bool Contains(Vector2 point) {
        return point.X >= Minimum.X &&
            point.X <= Maximum.X &&
            point.Y >= Minimum.Y &&
            point.Y <= Maximum.Y;
    }

    public bool Contains(Aabb2 other) {
        return other.Minimum.X >= Minimum.X &&
            other.Minimum.Y >= Minimum.Y &&
            other.Maximum.X <= Maximum.X &&
            other.Maximum.Y <= Maximum.Y;
    }

    public bool Intersects(Aabb2 other) {
        return Minimum.X <= other.Maximum.X &&
            Maximum.X >= other.Minimum.X &&
            Minimum.Y <= other.Maximum.Y &&
            Maximum.Y >= other.Minimum.Y;
    }

    public Aabb2 Union(Aabb2 other) {
        return new Aabb2(
            new Vector2(
                MathF.Min(Minimum.X, other.Minimum.X),
                MathF.Min(Minimum.Y, other.Minimum.Y)),
            new Vector2(
                MathF.Max(Maximum.X, other.Maximum.X),
                MathF.Max(Maximum.Y, other.Maximum.Y)));
    }

    public bool TryIntersect(Aabb2 other, out Aabb2 intersection) {
        return TryIntersect(other, out intersection, out _);
    }

    /// <summary>
    /// Attempts to intersect two bounding boxes and reports whether a result
    /// exists.
    /// </summary>
    public bool TryIntersect(
        Aabb2 other,
        out Aabb2 intersection,
        out GeometryOperationReport report) {
        if (!Intersects(other)) {
            intersection = default;
            report = new GeometryOperationReport(
                GeometryOperation.BoundsIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        intersection = new Aabb2(
            new Vector2(
                MathF.Max(Minimum.X, other.Minimum.X),
                MathF.Max(Minimum.Y, other.Minimum.Y)),
            new Vector2(
                MathF.Min(Maximum.X, other.Maximum.X),
                MathF.Min(Maximum.Y, other.Maximum.Y)));
        report = new GeometryOperationReport(
            GeometryOperation.BoundsIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    public Aabb2 Intersect(Aabb2 other) {
        if (!TryIntersect(other, out var intersection)) {
            throw new ArithmeticException(
                "The bounding boxes do not intersect.");
        }

        return intersection;
    }

    public Aabb2 Inflate(Vector2 amount) {
        if (!amount.IsFinite || amount.X < 0 || amount.Y < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Inflation amounts must be finite and non-negative.");
        }

        return new Aabb2(
            new Vector2(
                FloatingPointMath.SaturatingAdd(Minimum.X, -amount.X),
                FloatingPointMath.SaturatingAdd(Minimum.Y, -amount.Y)),
            new Vector2(
                FloatingPointMath.SaturatingAdd(Maximum.X, amount.X),
                FloatingPointMath.SaturatingAdd(Maximum.Y, amount.Y)));
    }

    public Aabb2 Inflate(float amount) {
        return Inflate(new Vector2(amount, amount));
    }

    public Vector2 ClosestPoint(Vector2 point) {
        return new Vector2(
            ScalarMath.Clamp(point.X, Minimum.X, Maximum.X),
            ScalarMath.Clamp(point.Y, Minimum.Y, Maximum.Y));
    }

    /// <summary>
    /// Gets the squared distance to the closed box. The value is positive
    /// infinity when its mathematical value exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float DistanceSquared(Vector2 point) {
        return Vector2.DistanceSquared(point, ClosestPoint(point));
    }

    /// <summary>
    /// Gets the distance to the closed box without first rounding a potentially
    /// overflowing squared distance to <see cref="float"/>.
    /// </summary>
    public float Distance(Vector2 point) {
        var closest = ClosestPoint(point);
        var x = (double)point.X - closest.X;
        var y = (double)point.Y - closest.Y;
        return (float)Math.Sqrt((x * x) + (y * y));
    }
}
