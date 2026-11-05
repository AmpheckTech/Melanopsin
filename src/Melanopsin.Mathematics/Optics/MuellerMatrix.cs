namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A four-by-four Mueller matrix in row-major logical storage and
/// column-Stokes-vector convention.
/// </summary>
/// <remarks>
/// No physical realizability constraints are imposed at construction. The type
/// represents intermediate algebraic matrices as well as physically realizable
/// polarization elements. For the Stokes-Mueller calculus, see Born and Wolf,
/// <i>Principles of Optics</i>, 7th ed. (1999), ch. 1.
/// </remarks>
public readonly partial record struct MuellerMatrix(
    double M11,
    double M12,
    double M13,
    double M14,
    double M21,
    double M22,
    double M23,
    double M24,
    double M31,
    double M32,
    double M33,
    double M34,
    double M41,
    double M42,
    double M43,
    double M44) {
    public static MuellerMatrix Identity => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public bool IsFinite =>
        double.IsFinite(M11) &&
        double.IsFinite(M12) &&
        double.IsFinite(M13) &&
        double.IsFinite(M14) &&
        double.IsFinite(M21) &&
        double.IsFinite(M22) &&
        double.IsFinite(M23) &&
        double.IsFinite(M24) &&
        double.IsFinite(M31) &&
        double.IsFinite(M32) &&
        double.IsFinite(M33) &&
        double.IsFinite(M34) &&
        double.IsFinite(M41) &&
        double.IsFinite(M42) &&
        double.IsFinite(M43) &&
        double.IsFinite(M44);

    public double this[int row, int column] {
        get {
            if ((uint)row >= 4 || (uint)column >= 4) {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            return (row * 4) + column switch {
                0 => M11,
                1 => M12,
                2 => M13,
                3 => M14,
                4 => M21,
                5 => M22,
                6 => M23,
                7 => M24,
                8 => M31,
                9 => M32,
                10 => M33,
                11 => M34,
                12 => M41,
                13 => M42,
                14 => M43,
                15 => M44,
                _ => throw new ArgumentOutOfRangeException(nameof(column)),
            };
        }
    }

    public StokesVector Transform(StokesVector value) {
        return new StokesVector(
            (M11 * value.I) + (M12 * value.Q) + (M13 * value.U) + (M14 * value.V),
            (M21 * value.I) + (M22 * value.Q) + (M23 * value.U) + (M24 * value.V),
            (M31 * value.I) + (M32 * value.Q) + (M33 * value.U) + (M34 * value.V),
            (M41 * value.I) + (M42 * value.Q) + (M43 * value.U) + (M44 * value.V));
    }

    public static MuellerMatrix Multiply(
        MuellerMatrix left,
        MuellerMatrix right) {
        return new MuellerMatrix(
            DotRowColumn(left, 0, right, 0),
            DotRowColumn(left, 0, right, 1),
            DotRowColumn(left, 0, right, 2),
            DotRowColumn(left, 0, right, 3),
            DotRowColumn(left, 1, right, 0),
            DotRowColumn(left, 1, right, 1),
            DotRowColumn(left, 1, right, 2),
            DotRowColumn(left, 1, right, 3),
            DotRowColumn(left, 2, right, 0),
            DotRowColumn(left, 2, right, 1),
            DotRowColumn(left, 2, right, 2),
            DotRowColumn(left, 2, right, 3),
            DotRowColumn(left, 3, right, 0),
            DotRowColumn(left, 3, right, 1),
            DotRowColumn(left, 3, right, 2),
            DotRowColumn(left, 3, right, 3));
    }

    public static StokesVector Multiply(
        MuellerMatrix matrix,
        StokesVector vector) {
        return matrix.Transform(vector);
    }

    public static MuellerMatrix operator *(
        MuellerMatrix left,
        MuellerMatrix right) {
        return Multiply(left, right);
    }

    public static StokesVector operator *(
        MuellerMatrix matrix,
        StokesVector vector) {
        return Multiply(matrix, vector);
    }

    private static double DotRowColumn(
        MuellerMatrix left,
        int row,
        MuellerMatrix right,
        int column) {
        return (left[row, 0] * right[0, column]) +
            (left[row, 1] * right[1, column]) +
            (left[row, 2] * right[2, column]) +
            (left[row, 3] * right[3, column]);
    }
}
