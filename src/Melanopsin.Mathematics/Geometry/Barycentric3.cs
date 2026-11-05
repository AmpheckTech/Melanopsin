namespace Melanopsin.Mathematics;

/// <summary>
/// Three affine barycentric weights associated with triangle vertices.
/// </summary>
/// <remarks>
/// The values are not automatically normalized. For affine interpolation the
/// weights should sum to one; <see cref="TryNormalize(out Barycentric3)"/> checks and normalizes
/// non-zero finite weights. The default value and unnormalized weights remain
/// meaningful for custom weighted blends but are not barycentric coordinates
/// in the strict affine sense.
/// </remarks>
public readonly record struct Barycentric3 {
    public Barycentric3(float first, float second, float third) {
        if (!float.IsFinite(first) ||
            !float.IsFinite(second) ||
            !float.IsFinite(third)) {
            throw new ArgumentOutOfRangeException(
                nameof(first),
                "Barycentric weights must be finite.");
        }

        First = first;
        Second = second;
        Third = third;
    }

    public float First { get; }

    public float Second { get; }

    public float Third { get; }

    public float Sum => First + Second + Third;

    public bool TryNormalize(out Barycentric3 normalized) {
        return TryNormalize(out normalized, out _);
    }

    /// <summary>
    /// Attempts affine normalization and reports a zero or non-finite sum.
    /// </summary>
    public bool TryNormalize(
        out Barycentric3 normalized,
        out GeometryOperationReport report) {
        var sum = Sum;
        if (!float.IsFinite(sum)) {
            normalized = default;
            report = new GeometryOperationReport(
                GeometryOperation.BarycentricNormalization,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (sum == 0) {
            normalized = default;
            report = new GeometryOperationReport(
                GeometryOperation.BarycentricNormalization,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        normalized = new Barycentric3(
            First / sum,
            Second / sum,
            Third / sum);
        report = new GeometryOperationReport(
            GeometryOperation.BarycentricNormalization,
            GeometryStatus.Succeeded);
        return true;
    }

    public Barycentric3 Normalize() {
        if (!TryNormalize(out var normalized)) {
            throw new ArithmeticException(
                "Barycentric weights cannot be normalized when their sum is zero or non-finite.");
        }

        return normalized;
    }

    public Vector3 Interpolate(Triangle3 triangle) {
        return (triangle.First * First) +
            (triangle.Second * Second) +
            (triangle.Third * Third);
    }

    public static Barycentric3 FromPoint(Triangle3 triangle, Vector3 point) {
        var weights = triangle.Barycentric(point);
        return new Barycentric3(weights.X, weights.Y, weights.Z);
    }
}
