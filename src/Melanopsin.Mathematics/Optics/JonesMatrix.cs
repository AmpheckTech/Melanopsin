namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A two-by-two Jones matrix in row-major logical storage and column-vector
/// convention.
/// </summary>
/// <remarks>
/// The matrix acts on a <see cref="JonesVector"/> as
/// <c>[X'; Y'] = [[XX, XY], [YX, YY]] [X; Y]</c>. Physical reciprocity,
/// losslessness, and basis interpretation are properties of the caller's
/// model, not implied by this value type. For the Jones calculus, see Born and
/// Wolf, <i>Principles of Optics</i>, 7th ed. (1999), ch. 1.
/// </remarks>
public readonly partial record struct JonesMatrix(
    Complex XX,
    Complex XY,
    Complex YX,
    Complex YY) {
    public static JonesMatrix Identity => new(
        Complex.One,
        Complex.Zero,
        Complex.Zero,
        Complex.One);

    public Complex Determinant => (XX * YY) - (XY * YX);

    public Complex Trace => XX + YY;

    public bool IsFinite =>
        XX.IsFinite && XY.IsFinite && YX.IsFinite && YY.IsFinite;

    public JonesVector Transform(JonesVector value) {
        return new JonesVector(
            (XX * value.X) + (XY * value.Y),
            (YX * value.X) + (YY * value.Y));
    }

    public JonesMatrix Transpose() => new(XX, YX, XY, YY);

    public JonesMatrix Conjugate() => new(
        XX.Conjugate(),
        XY.Conjugate(),
        YX.Conjugate(),
        YY.Conjugate());

    public JonesMatrix ConjugateTranspose() => new(
        XX.Conjugate(),
        YX.Conjugate(),
        XY.Conjugate(),
        YY.Conjugate());

    public bool TryInvert(out JonesMatrix inverse) {
        return TryInvert(out inverse, out _);
    }

    public JonesMatrix Invert() {
        if (!TryInvert(out var result)) {
            throw new ArithmeticException(
                "The Jones matrix is singular or its inverse is non-finite.");
        }

        return result;
    }

    public static JonesMatrix Multiply(
        JonesMatrix left,
        JonesMatrix right) {
        return new JonesMatrix(
            (left.XX * right.XX) + (left.XY * right.YX),
            (left.XX * right.XY) + (left.XY * right.YY),
            (left.YX * right.XX) + (left.YY * right.YX),
            (left.YX * right.XY) + (left.YY * right.YY));
    }

    public static JonesVector Multiply(
        JonesMatrix matrix,
        JonesVector vector) {
        return matrix.Transform(vector);
    }

    public static JonesMatrix operator *(JonesMatrix left, JonesMatrix right) {
        return Multiply(left, right);
    }

    public static JonesVector operator *(JonesMatrix matrix, JonesVector vector) {
        return Multiply(matrix, vector);
    }
}
