using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-component vector over <see cref="Complex"/> values.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ComplexVector2 :
    IAdditionOperators<ComplexVector2, ComplexVector2, ComplexVector2>,
    ISubtractionOperators<ComplexVector2, ComplexVector2, ComplexVector2>,
    IUnaryNegationOperators<ComplexVector2, ComplexVector2>,
    IMultiplyOperators<ComplexVector2, Complex, ComplexVector2>,
    IDivisionOperators<ComplexVector2, Complex, ComplexVector2>,
    IAdditiveIdentity<ComplexVector2, ComplexVector2>,
    IEqualityOperators<ComplexVector2, ComplexVector2, bool> {
    public ComplexVector2(Complex x, Complex y) {
        X = x;
        Y = y;
    }

    public static ComplexVector2 Zero => default;

    public static ComplexVector2 One => new(Complex.One, Complex.One);

    public static ComplexVector2 UnitX => new(Complex.One, Complex.Zero);

    public static ComplexVector2 UnitY => new(Complex.Zero, Complex.One);

    public static ComplexVector2 AdditiveIdentity => Zero;

    public Complex X { get; }

    public Complex Y { get; }

    public double LengthSquared {
        get {
            var length = Length;
            return length * length;
        }
    }

    public double Length => EuclideanNorm(X, Y);

    public bool IsFinite => ComplexMath.IsFinite(X) && ComplexMath.IsFinite(Y);

    /// <summary>
    /// Returns the bilinear dot product without conjugating either argument.
    /// Use <see cref="InnerProduct"/> for the Hermitian inner product.
    /// </summary>
    public static Complex Dot(
        ComplexVector2 left,
        ComplexVector2 right) {
        return (left.X * right.X) + (left.Y * right.Y);
    }

    /// <summary>
    /// Returns the Hermitian inner product, which conjugates the left
    /// argument.
    /// </summary>
    public static Complex InnerProduct(
        ComplexVector2 left,
        ComplexVector2 right) {
        return Dot(left.Conjugate(), right);
    }

    public ComplexVector2 Conjugate() {
        return new ComplexVector2(X.Conjugate(), Y.Conjugate());
    }

    public ComplexVector2 Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out ComplexVector2 result) {
        return TryNormalize(out result, out _);
    }

    /// <summary>
    /// Attempts to normalize the vector and reports non-finite or degenerate
    /// input without applying an absolute near-zero threshold.
    /// </summary>
    public bool TryNormalize(
        out ComplexVector2 result,
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

    public static ComplexVector2 Add(
        ComplexVector2 left,
        ComplexVector2 right) {
        return new ComplexVector2(left.X + right.X, left.Y + right.Y);
    }

    public static ComplexVector2 Subtract(
        ComplexVector2 left,
        ComplexVector2 right) {
        return new ComplexVector2(left.X - right.X, left.Y - right.Y);
    }

    public static ComplexVector2 Negate(ComplexVector2 value) {
        return new ComplexVector2(-value.X, -value.Y);
    }

    public static ComplexVector2 Multiply(
        ComplexVector2 value,
        Complex scale) {
        return new ComplexVector2(value.X * scale, value.Y * scale);
    }

    public static ComplexVector2 Multiply(
        Complex scale,
        ComplexVector2 value) {
        return Multiply(value, scale);
    }

    public static ComplexVector2 Divide(
        ComplexVector2 value,
        Complex divisor) {
        return new ComplexVector2(value.X / divisor, value.Y / divisor);
    }

    public static ComplexVector2 operator +(
        ComplexVector2 left,
        ComplexVector2 right) {
        return Add(left, right);
    }

    public static ComplexVector2 operator -(
        ComplexVector2 left,
        ComplexVector2 right) {
        return Subtract(left, right);
    }

    public static ComplexVector2 operator -(ComplexVector2 value) {
        return Negate(value);
    }

    public static ComplexVector2 operator *(
        ComplexVector2 value,
        Complex scale) {
        return Multiply(value, scale);
    }

    public static ComplexVector2 operator *(
        Complex scale,
        ComplexVector2 value) {
        return Multiply(scale, value);
    }

    public static ComplexVector2 operator /(
        ComplexVector2 value,
        Complex divisor) {
        return Divide(value, divisor);
    }

    private static double EuclideanNorm(Complex first, Complex second) {
        var firstMagnitude = first.Magnitude;
        var secondMagnitude = second.Magnitude;
        var scale = Math.Max(firstMagnitude, secondMagnitude);
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
        return scale * Math.Sqrt(
            (scaledFirst * scaledFirst) + (scaledSecond * scaledSecond));
    }
}
