namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A seven-parameter Helmert transformation between geocentric Cartesian
/// reference frames.
/// </summary>
/// <remarks>
/// <para>
/// A source position <c>s</c> is transformed as <c>t + (1 + dS) R s</c>,
/// where <c>t</c> is the translation, <c>dS</c> is the scale difference, and
/// <c>R</c> is the first-order small-angle rotation matrix. Translations use
/// the same length unit as the coordinates (normally metres), rotations are in
/// arc-seconds, and the scale difference is in parts per million.
/// </para>
/// <para>
/// This is the linearized seven-parameter model used by EPSG methods 1032
/// (coordinate frame rotation) and 1033 (position vector transformation), not
/// an exact finite-rotation matrix. The rotation convention is part of the
/// transform definition and must match the source of the parameters.
/// </para>
/// <para>
/// The transform is directional. Use <see cref="TransformInverse"/> only for
/// the algebraic inverse of this particular linear map. An authority may
/// publish a different reverse transformation or a non-rigorous grid-based
/// transformation for the opposite direction.
/// </para>
/// </remarks>
public readonly record struct HelmertTransform {
    /// <summary>
    /// Initializes a seven-parameter Helmert transform.
    /// </summary>
    /// <param name="translationX">The X translation.</param>
    /// <param name="translationY">The Y translation.</param>
    /// <param name="translationZ">The Z translation.</param>
    /// <param name="rotationXArcSeconds">The X-axis rotation in arc-seconds.</param>
    /// <param name="rotationYArcSeconds">The Y-axis rotation in arc-seconds.</param>
    /// <param name="rotationZArcSeconds">The Z-axis rotation in arc-seconds.</param>
    /// <param name="scalePpm">The scale difference in parts per million.</param>
    /// <param name="convention">The rotation sign convention.</param>
    public HelmertTransform(
        double translationX,
        double translationY,
        double translationZ,
        double rotationXArcSeconds,
        double rotationYArcSeconds,
        double rotationZArcSeconds,
        double scalePpm,
        HelmertRotationConvention convention =
            HelmertRotationConvention.PositionVector) {
        if (!double.IsFinite(translationX)) {
            throw new ArgumentOutOfRangeException(nameof(translationX));
        }

        if (!double.IsFinite(translationY)) {
            throw new ArgumentOutOfRangeException(nameof(translationY));
        }

        if (!double.IsFinite(translationZ)) {
            throw new ArgumentOutOfRangeException(nameof(translationZ));
        }

        if (!double.IsFinite(rotationXArcSeconds)) {
            throw new ArgumentOutOfRangeException(nameof(rotationXArcSeconds));
        }

        if (!double.IsFinite(rotationYArcSeconds)) {
            throw new ArgumentOutOfRangeException(nameof(rotationYArcSeconds));
        }

        if (!double.IsFinite(rotationZArcSeconds)) {
            throw new ArgumentOutOfRangeException(nameof(rotationZArcSeconds));
        }

        if (!double.IsFinite(scalePpm) || scalePpm <= -1_000_000) {
            throw new ArgumentOutOfRangeException(nameof(scalePpm));
        }

        if (!Enum.IsDefined(convention)) {
            throw new ArgumentOutOfRangeException(nameof(convention));
        }

        TranslationX = translationX;
        TranslationY = translationY;
        TranslationZ = translationZ;
        RotationXArcSeconds = rotationXArcSeconds;
        RotationYArcSeconds = rotationYArcSeconds;
        RotationZArcSeconds = rotationZArcSeconds;
        ScalePpm = scalePpm;
        Convention = convention;
    }

    /// <summary>Gets the identity transform.</summary>
    public static HelmertTransform Identity => default;

    public double TranslationX { get; }

    public double TranslationY { get; }

    public double TranslationZ { get; }

    public double RotationXArcSeconds { get; }

    public double RotationYArcSeconds { get; }

    public double RotationZArcSeconds { get; }

    public double ScalePpm { get; }

    public HelmertRotationConvention Convention { get; }

    /// <summary>
    /// Transforms a source geocentric coordinate into the target frame.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The transformed coordinate is not finite.
    /// </exception>
    public EcefCoordinate Transform(EcefCoordinate source) {
        if (!TryTransform(source, out var result)) {
            throw new ArithmeticException(
                "The Helmert transform produced a non-finite coordinate.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a source geocentric coordinate into the target
    /// frame without throwing for non-finite results.
    /// </summary>
    public bool TryTransform(
        EcefCoordinate source,
        out EcefCoordinate result) {
        return TryTransform(source, out result, out _);
    }

    /// <summary>
    /// Attempts to transform a source geocentric coordinate and reports the
    /// terminating numerical condition.
    /// </summary>
    public bool TryTransform(
        EcefCoordinate source,
        out EcefCoordinate result,
        out CoordinateOperationReport report) {
        if (!IsFinite(source)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertForward,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var (a11, a12, a13, a21, a22, a23, a31, a32, a33) =
            CreateLinearMatrix();
        var x = TranslationX +
            (a11 * source.X) +
            (a12 * source.Y) +
            (a13 * source.Z);
        var y = TranslationY +
            (a21 * source.X) +
            (a22 * source.Y) +
            (a23 * source.Z);
        var z = TranslationZ +
            (a31 * source.X) +
            (a32 * source.Y) +
            (a33 * source.Z);
        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertForward,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new EcefCoordinate(x, y, z);
        report = new CoordinateOperationReport(
            CoordinateOperation.HelmertForward,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Applies the algebraic inverse of this transform to a target-frame
    /// coordinate.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The linear part is exactly singular in double precision, or the result
    /// is not finite.
    /// </exception>
    public EcefCoordinate TransformInverse(EcefCoordinate target) {
        if (!TryTransformInverse(target, out var result)) {
            throw new ArithmeticException(
                "The inverse Helmert transform is singular or non-finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to apply the algebraic inverse of this transform.
    /// </summary>
    /// <remarks>
    /// No absolute near-singularity threshold is applied. A singular failure
    /// means the relevant determinant is exactly zero in double precision;
    /// non-finite intermediate values are reported separately as
    /// <see cref="CoordinateOperationStatus.NonFiniteOutput"/>. A successful
    /// result does not by itself guarantee a particular accuracy; callers
    /// should assess conditioning when non-standard rotation or scale
    /// parameters are used.
    /// </remarks>
    public bool TryTransformInverse(
        EcefCoordinate target,
        out EcefCoordinate result) {
        return TryTransformInverse(target, out result, out _);
    }

    /// <summary>
    /// Attempts to apply the algebraic inverse and reports singularity or
    /// non-finite intermediate conditions.
    /// </summary>
    public bool TryTransformInverse(
        EcefCoordinate target,
        out EcefCoordinate result,
        out CoordinateOperationReport report) {
        if (!IsFinite(target)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertInverse,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var (a11, a12, a13, a21, a22, a23, a31, a32, a33) =
            CreateLinearMatrix();
        var determinant =
            (a11 * ((a22 * a33) - (a23 * a32))) -
            (a12 * ((a21 * a33) - (a23 * a31))) +
            (a13 * ((a21 * a32) - (a22 * a31)));
        if (determinant == 0) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertInverse,
                CoordinateOperationStatus.Singular);
            return false;
        }

        if (!double.IsFinite(determinant)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        var inverseDeterminant = 1 / determinant;
        var i11 = ((a22 * a33) - (a23 * a32)) * inverseDeterminant;
        var i12 = ((a13 * a32) - (a12 * a33)) * inverseDeterminant;
        var i13 = ((a12 * a23) - (a13 * a22)) * inverseDeterminant;
        var i21 = ((a23 * a31) - (a21 * a33)) * inverseDeterminant;
        var i22 = ((a11 * a33) - (a13 * a31)) * inverseDeterminant;
        var i23 = ((a13 * a21) - (a11 * a23)) * inverseDeterminant;
        var i31 = ((a21 * a32) - (a22 * a31)) * inverseDeterminant;
        var i32 = ((a12 * a31) - (a11 * a32)) * inverseDeterminant;
        var i33 = ((a11 * a22) - (a12 * a21)) * inverseDeterminant;
        var deltaX = target.X - TranslationX;
        var deltaY = target.Y - TranslationY;
        var deltaZ = target.Z - TranslationZ;
        var x = (i11 * deltaX) + (i12 * deltaY) + (i13 * deltaZ);
        var y = (i21 * deltaX) + (i22 * deltaY) + (i23 * deltaZ);
        var z = (i31 * deltaX) + (i32 * deltaY) + (i33 * deltaZ);
        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.HelmertInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new EcefCoordinate(x, y, z);
        report = new CoordinateOperationReport(
            CoordinateOperation.HelmertInverse,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    private static bool IsFinite(EcefCoordinate coordinate) {
        return double.IsFinite(coordinate.X) &&
            double.IsFinite(coordinate.Y) &&
            double.IsFinite(coordinate.Z);
    }

    private (double A11,
        double A12,
        double A13,
        double A21,
        double A22,
        double A23,
        double A31,
        double A32,
        double A33) CreateLinearMatrix() {
        const double radiansPerArcSecond = Math.PI / 648_000;
        const double partsPerMillion = 1e-6;
        var rotationX = RotationXArcSeconds * radiansPerArcSecond;
        var rotationY = RotationYArcSeconds * radiansPerArcSecond;
        var rotationZ = RotationZArcSeconds * radiansPerArcSecond;
        var scale = 1 + (ScalePpm * partsPerMillion);
        if (Convention == HelmertRotationConvention.PositionVector) {
            return (
                scale, -scale * rotationZ, scale * rotationY,
                scale * rotationZ, scale, -scale * rotationX,
                -scale * rotationY, scale * rotationX, scale);
        }

        return (
            scale, scale * rotationZ, -scale * rotationY,
            -scale * rotationZ, scale, scale * rotationX,
            scale * rotationY, -scale * rotationX, scale);
    }
}
