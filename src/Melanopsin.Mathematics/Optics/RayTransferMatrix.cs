namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A paraxial ray represented by transverse height followed by angle relative
/// to the local positive optical axis.
/// </summary>
public readonly record struct ParaxialRay(double Height, double Angle) {
    /// <summary>Gets whether both coordinates are finite.</summary>
    public bool IsFinite => double.IsFinite(Height) && double.IsFinite(Angle);
}

/// <summary>
/// A real two-by-two paraxial ray-transfer matrix using the column-vector
/// convention <c>[height'; angle'] = [[A,B],[C,D]] [height; angle]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The matrix is intentionally independent of a global coordinate frame. A
/// caller assigns positive propagation direction and transverse-axis orientation
/// through the adjacent <see cref="Melanopsin.Mathematics.Coordinates.CoordinateFrame3"/> or another explicit
/// convention. Cascades multiply from right to left:
/// <c>M_total = M_n ... M_2 M_1</c>.
/// </para>
/// <para>
/// For a rotationally symmetric paraxial system the determinant is the
/// refractive-index ratio between input and output spaces. The presets here
/// cover free space, an ideal thin lens, and a single spherical refracting
/// interface. The paraxial transfer-matrix formulation follows Born and Wolf,
/// <i>Principles of Optics</i>, 7th ed. (1999), ch. 4.
/// </para>
/// </remarks>
public readonly partial record struct RayTransferMatrix(
    double A,
    double B,
    double C,
    double D) {
    /// <summary>Gets the identity ray-transfer matrix.</summary>
    public static RayTransferMatrix Identity => new(1, 0, 0, 1);

    /// <summary>Gets the determinant <c>AD-BC</c>.</summary>
    public double Determinant => (A * D) - (B * C);

    /// <summary>Gets whether every matrix entry is finite.</summary>
    public bool IsFinite =>
        double.IsFinite(A) &&
        double.IsFinite(B) &&
        double.IsFinite(C) &&
        double.IsFinite(D);

    /// <summary>Transforms a paraxial ray.</summary>
    public ParaxialRay Transform(ParaxialRay ray) {
        return new ParaxialRay(
            (A * ray.Height) + (B * ray.Angle),
            (C * ray.Height) + (D * ray.Angle));
    }

    /// <summary>Creates a translation through a homogeneous medium.</summary>
    /// <param name="length">Positive propagation length along the local axis.</param>
    public static RayTransferMatrix FreeSpace(double length) {
        if (!double.IsFinite(length)) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new RayTransferMatrix(1, length, 0, 1);
    }

    /// <summary>Creates an ideal thin lens.</summary>
    public static RayTransferMatrix ThinLens(double focalLength) {
        if (!double.IsFinite(focalLength) || focalLength == 0) {
            throw new ArgumentOutOfRangeException(nameof(focalLength));
        }

        return new RayTransferMatrix(1, 0, -1 / focalLength, 1);
    }

    /// <summary>
    /// Creates a paraxial spherical refracting interface.
    /// </summary>
    /// <param name="incidentIndex">The positive refractive index before the interface.</param>
    /// <param name="exitingIndex">The positive refractive index after the interface.</param>
    /// <param name="radiusOfCurvature">
    /// The signed radius. A positive value places the center of curvature on the
    /// positive-axis side of the vertex; a negative value places it on the
    /// incident side. A plane interface uses positive infinity.
    /// </param>
    public static RayTransferMatrix SphericalRefraction(
        double incidentIndex,
        double exitingIndex,
        double radiusOfCurvature) {
        if (!double.IsFinite(incidentIndex) || incidentIndex <= 0) {
            throw new ArgumentOutOfRangeException(nameof(incidentIndex));
        }

        if (!double.IsFinite(exitingIndex) || exitingIndex <= 0) {
            throw new ArgumentOutOfRangeException(nameof(exitingIndex));
        }

        if (double.IsNaN(radiusOfCurvature) ||
            radiusOfCurvature == 0 ||
            !double.IsFinite(radiusOfCurvature) &&
                !double.IsPositiveInfinity(radiusOfCurvature)) {
            throw new ArgumentOutOfRangeException(nameof(radiusOfCurvature));
        }

        var power = double.IsInfinity(radiusOfCurvature)
            ? 0
            : (exitingIndex - incidentIndex) / radiusOfCurvature;
        return new RayTransferMatrix(
            1,
            0,
            -power / exitingIndex,
            incidentIndex / exitingIndex);
    }

    /// <summary>Computes <c>left * right</c> for cascaded ray transfers.</summary>
    public static RayTransferMatrix Multiply(
        RayTransferMatrix left,
        RayTransferMatrix right) {
        return new RayTransferMatrix(
            (left.A * right.A) + (left.B * right.C),
            (left.A * right.B) + (left.B * right.D),
            (left.C * right.A) + (left.D * right.C),
            (left.C * right.B) + (left.D * right.D));
    }

    /// <summary>Attempts to invert a matrix whose determinant is exactly nonzero.</summary>
    public bool TryInvert(out RayTransferMatrix inverse) {
        return TryInvert(out inverse, out _);
    }

    /// <summary>Inverts the matrix or throws when it is singular.</summary>
    public RayTransferMatrix Invert() {
        if (!TryInvert(out var inverse)) {
            throw new ArithmeticException(
                "The ray-transfer matrix is singular or its inverse is not finite.");
        }

        return inverse;
    }

    /// <summary>Multiplies two cascaded ray-transfer matrices.</summary>
    public static RayTransferMatrix operator *(
        RayTransferMatrix left,
        RayTransferMatrix right) {
        return Multiply(left, right);
    }

    /// <summary>Transforms a paraxial ray.</summary>
    public static ParaxialRay operator *(
        RayTransferMatrix matrix,
        ParaxialRay ray) {
        return matrix.Transform(ray);
    }
}
