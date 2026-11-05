// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-component vector over <see cref="Complex"/> values.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ComplexVector3 :
    IAdditionOperators<ComplexVector3, ComplexVector3, ComplexVector3>,
    ISubtractionOperators<ComplexVector3, ComplexVector3, ComplexVector3>,
    IUnaryNegationOperators<ComplexVector3, ComplexVector3>,
    IMultiplyOperators<ComplexVector3, Complex, ComplexVector3>,
    IDivisionOperators<ComplexVector3, Complex, ComplexVector3>,
    IAdditiveIdentity<ComplexVector3, ComplexVector3>,
    IEqualityOperators<ComplexVector3, ComplexVector3, bool> {
    public ComplexVector3(Complex x, Complex y, Complex z) {
        X = x;
        Y = y;
        Z = z;
    }

    public static ComplexVector3 Zero => default;

    public static ComplexVector3 One => new(Complex.One, Complex.One, Complex.One);

    public static ComplexVector3 UnitX => new(Complex.One, Complex.Zero, Complex.Zero);

    public static ComplexVector3 UnitY => new(Complex.Zero, Complex.One, Complex.Zero);

    public static ComplexVector3 UnitZ => new(Complex.Zero, Complex.Zero, Complex.One);

    public static ComplexVector3 AdditiveIdentity => Zero;

    public Complex X { get; }

    public Complex Y { get; }

    public Complex Z { get; }

    public double LengthSquared {
        get {
            var length = Length;
            return length * length;
        }
    }

    public double Length => EuclideanNorm(X, Y, Z);

    public bool IsFinite =>
        ComplexMath.IsFinite(X) &&
        ComplexMath.IsFinite(Y) &&
        ComplexMath.IsFinite(Z);

    /// <summary>
    /// Returns the bilinear dot product without conjugating either argument.
    /// Use <see cref="InnerProduct"/> for the Hermitian inner product.
    /// </summary>
    public static Complex Dot(
        ComplexVector3 left,
        ComplexVector3 right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z);
    }

    /// <summary>
    /// Returns the Hermitian inner product, which conjugates the left
    /// argument.
    /// </summary>
    public static Complex InnerProduct(
        ComplexVector3 left,
        ComplexVector3 right) {
        return Dot(left.Conjugate(), right);
    }

    /// <summary>
    /// Returns the bilinear three-dimensional cross product.
    /// </summary>
    public static ComplexVector3 Cross(
        ComplexVector3 left,
        ComplexVector3 right) {
        return new ComplexVector3(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));
    }

    public ComplexVector3 Conjugate() {
        return new ComplexVector3(
            X.Conjugate(),
            Y.Conjugate(),
            Z.Conjugate());
    }

    public ComplexVector3 Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out ComplexVector3 result) {
        return TryNormalize(out result, out _);
    }

    /// <summary>
    /// Attempts to normalize the vector and reports non-finite or degenerate
    /// input without applying an absolute near-zero threshold.
    /// </summary>
    public bool TryNormalize(
        out ComplexVector3 result,
        out LinearAlgebraOperationReport report) {
        if (!IsFinite) {
            result = Zero;
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexVectorNormalization,
                LinearAlgebraStatus.NonFiniteInput);
            return false;
        }

        var length = Length;
        if (length == 0) {
            result = Zero;
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexVectorNormalization,
                LinearAlgebraStatus.DegenerateInput);
            return false;
        }

        result = Divide(this, new Complex(length, 0));
        if (!result.IsFinite) {
            result = Zero;
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexVectorNormalization,
                LinearAlgebraStatus.NonFiniteOutput);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexVectorNormalization,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    public static ComplexVector3 Add(
        ComplexVector3 left,
        ComplexVector3 right) {
        return new ComplexVector3(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    public static ComplexVector3 Subtract(
        ComplexVector3 left,
        ComplexVector3 right) {
        return new ComplexVector3(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }

    public static ComplexVector3 Negate(ComplexVector3 value) {
        return new ComplexVector3(-value.X, -value.Y, -value.Z);
    }

    public static ComplexVector3 Multiply(
        ComplexVector3 value,
        Complex scale) {
        return new ComplexVector3(
            value.X * scale,
            value.Y * scale,
            value.Z * scale);
    }

    public static ComplexVector3 Multiply(
        Complex scale,
        ComplexVector3 value) {
        return Multiply(value, scale);
    }

    public static ComplexVector3 Divide(
        ComplexVector3 value,
        Complex divisor) {
        return new ComplexVector3(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor);
    }

    public static ComplexVector3 operator +(
        ComplexVector3 left,
        ComplexVector3 right) {
        return Add(left, right);
    }

    public static ComplexVector3 operator -(
        ComplexVector3 left,
        ComplexVector3 right) {
        return Subtract(left, right);
    }

    public static ComplexVector3 operator -(ComplexVector3 value) {
        return Negate(value);
    }

    public static ComplexVector3 operator *(
        ComplexVector3 value,
        Complex scale) {
        return Multiply(value, scale);
    }

    public static ComplexVector3 operator *(
        Complex scale,
        ComplexVector3 value) {
        return Multiply(scale, value);
    }

    public static ComplexVector3 operator /(
        ComplexVector3 value,
        Complex divisor) {
        return Divide(value, divisor);
    }

    private static double EuclideanNorm(
        Complex first,
        Complex second,
        Complex third) {
        var firstMagnitude = first.Magnitude;
        var secondMagnitude = second.Magnitude;
        var thirdMagnitude = third.Magnitude;
        var scale = Math.Max(
            firstMagnitude,
            Math.Max(secondMagnitude, thirdMagnitude));
        if (scale == 0) {
            return 0;
        }

        if (double.IsNaN(scale)) {
            return double.NaN;
        }

        if (!double.IsFinite(scale)) {
            return double.PositiveInfinity;
        }

        var scaledFirst = firstMagnitude / scale;
        var scaledSecond = secondMagnitude / scale;
        var scaledThird = thirdMagnitude / scale;
        return scale * Math.Sqrt(
            (scaledFirst * scaledFirst) +
            (scaledSecond * scaledSecond) +
            (scaledThird * scaledThird));
    }
}
