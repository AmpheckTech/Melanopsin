// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A two-component Jones polarization vector with column-vector convention.
/// </summary>
/// <remarks>
/// The components are complex electric-field amplitudes in an explicitly
/// chosen transverse basis. The type does not assume that the basis is
/// horizontal/vertical; name basis changes at the call site. Stokes conversion
/// uses <c>I=|X|^2+|Y|^2</c>, <c>Q=|X|^2-|Y|^2</c>,
/// <c>U=2 Re(X Y*)</c>, and <c>V=-2 Im(X Y*)</c>. For polarization conventions
/// and the Jones calculus, see Born and Wolf, <i>Principles of Optics</i>, 7th
/// ed. (1999), ch. 1.
/// </remarks>
public readonly partial record struct JonesVector(Complex X, Complex Y) {
    public static JonesVector Zero => default;

    public double Intensity => X.MagnitudeSquared + Y.MagnitudeSquared;

    public bool IsFinite => X.IsFinite && Y.IsFinite;

    public StokesVector ToStokes() {
        var cross = X * Y.Conjugate();
        return new StokesVector(
            X.MagnitudeSquared + Y.MagnitudeSquared,
            X.MagnitudeSquared - Y.MagnitudeSquared,
            2 * cross.Real,
            -2 * cross.Imaginary);
    }

    public JonesVector Conjugate() => new(X.Conjugate(), Y.Conjugate());

    public bool TryNormalize(out JonesVector result) {
        return TryNormalize(out result, out _);
    }

    public JonesVector Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite Jones vector cannot be normalized.");
        }

        return result;
    }

    public static JonesVector Add(JonesVector left, JonesVector right) {
        return new JonesVector(left.X + right.X, left.Y + right.Y);
    }

    public static JonesVector Subtract(JonesVector left, JonesVector right) {
        return new JonesVector(left.X - right.X, left.Y - right.Y);
    }

    public static JonesVector Multiply(JonesVector value, Complex scale) {
        return new JonesVector(value.X * scale, value.Y * scale);
    }

    public static JonesVector operator +(JonesVector left, JonesVector right) {
        return Add(left, right);
    }

    public static JonesVector operator -(JonesVector left, JonesVector right) {
        return Subtract(left, right);
    }

    public static JonesVector operator *(JonesVector value, Complex scale) {
        return Multiply(value, scale);
    }
}
