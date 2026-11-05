namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A Stokes polarization vector <c>[I, Q, U, V]</c> with no implicit
/// normalization.
/// </summary>
/// <remarks>
/// <c>I</c> is total intensity, <c>Q</c> is horizontal minus vertical linear
/// intensity, <c>U</c> is +45 minus -45 degree linear intensity, and <c>V</c>
/// is right minus left circular intensity for the convention documented by
/// <see cref="JonesVector.ToStokes"/>. Physically realizable vectors satisfy
/// <c>I &gt;= 0</c> and <c>Q^2 + U^2 + V^2 &lt;= I^2</c>; construction does
/// not enforce those constraints so intermediate algebraic states remain
/// representable. For polarization conventions, see Born and Wolf,
/// <i>Principles of Optics</i>, 7th ed. (1999), ch. 1.
/// </remarks>
public readonly partial record struct StokesVector(
    double I,
    double Q,
    double U,
    double V) {
    public static StokesVector Zero => default;

    public bool IsFinite =>
        double.IsFinite(I) &&
        double.IsFinite(Q) &&
        double.IsFinite(U) &&
        double.IsFinite(V);

    /// <summary>
    /// Gets the degree of polarization, or NaN when total intensity is zero.
    /// </summary>
    public double DegreeOfPolarization {
        get {
            if (I == 0) {
                return double.NaN;
            }

            var polarized = Math.Sqrt((Q * Q) + (U * U) + (V * V));
            return polarized / Math.Abs(I);
        }
    }

    public static StokesVector Add(StokesVector left, StokesVector right) {
        return new StokesVector(
            left.I + right.I,
            left.Q + right.Q,
            left.U + right.U,
            left.V + right.V);
    }

    public static StokesVector Multiply(StokesVector value, double scale) {
        return new StokesVector(
            value.I * scale,
            value.Q * scale,
            value.U * scale,
            value.V * scale);
    }

    public static StokesVector operator +(StokesVector left, StokesVector right) {
        return Add(left, right);
    }

    public static StokesVector Subtract(StokesVector left, StokesVector right) {
        return new StokesVector(
            left.I - right.I,
            left.Q - right.Q,
            left.U - right.U,
            left.V - right.V);
    }

    public static StokesVector operator -(StokesVector left, StokesVector right) {
        return Subtract(left, right);
    }

    public static StokesVector operator *(StokesVector value, double scale) {
        return Multiply(value, scale);
    }

    public static StokesVector operator *(double scale, StokesVector value) {
        return Multiply(value, scale);
    }
}
