// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable polynomial with real coefficients in ascending degree order.
/// </summary>
/// <remarks>
/// The coefficient at index <c>i</c> multiplies <c>x^i</c>. Trailing zero
/// coefficients do not affect degree semantics and are removed when the
/// instance is constructed. The zero polynomial has degree <c>-1</c>.
/// </remarks>
public sealed class Polynomial :
    IEquatable<Polynomial> {
    private readonly double[] _coefficients;

    public Polynomial(ReadOnlySpan<double> coefficients) {
        var degree = coefficients.Length - 1;
        while (degree >= 0 && coefficients[degree] == 0) {
            degree--;
        }

        if (degree < 0) {
            _coefficients = [];
            return;
        }

        _coefficients = coefficients[..(degree + 1)].ToArray();
        for (var index = 0; index < _coefficients.Length; index++) {
            if (!double.IsFinite(_coefficients[index])) {
                throw new ArgumentException(
                    "Polynomial coefficients must be finite.",
                    nameof(coefficients));
            }
        }
    }

    public static Polynomial Zero => new([]);

    public static Polynomial One => new([1]);

    public int Degree => _coefficients.Length - 1;

    public bool IsZero => _coefficients.Length == 0;

    public ReadOnlySpan<double> Coefficients => _coefficients;

    public double this[int degree] {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(degree);
            return degree < _coefficients.Length ? _coefficients[degree] : 0;
        }
    }

    public double LeadingCoefficient => IsZero ? 0 : _coefficients[^1];

    public double Evaluate(double value) {
        var result = 0.0;
        for (var index = _coefficients.Length - 1; index >= 0; index--) {
            result = (result * value) + _coefficients[index];
        }

        return result;
    }

    public Complex Evaluate(Complex value) {
        var result = Complex.Zero;
        for (var index = _coefficients.Length - 1; index >= 0; index--) {
            result = (result * value) + _coefficients[index];
        }

        return result;
    }

    public Polynomial Derivative() {
        if (Degree <= 0) {
            return Zero;
        }

        var result = new double[Degree];
        for (var index = 1; index < _coefficients.Length; index++) {
            result[index - 1] = _coefficients[index] * index;
        }

        return new Polynomial(result);
    }

    public Polynomial Integral(double constant = 0) {
        if (!double.IsFinite(constant)) {
            throw new ArgumentOutOfRangeException(nameof(constant));
        }

        var result = new double[checked(_coefficients.Length + 1)];
        result[0] = constant;
        for (var index = 0; index < _coefficients.Length; index++) {
            result[index + 1] = _coefficients[index] / (index + 1);
        }

        return new Polynomial(result);
    }

    public Polynomial Add(Polynomial other) {
        ArgumentNullException.ThrowIfNull(other);
        var length = Math.Max(_coefficients.Length, other._coefficients.Length);
        var result = new double[length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = this[index] + other[index];
        }

        return new Polynomial(result);
    }

    public Polynomial Subtract(Polynomial other) {
        ArgumentNullException.ThrowIfNull(other);
        var length = Math.Max(_coefficients.Length, other._coefficients.Length);
        var result = new double[length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = this[index] - other[index];
        }

        return new Polynomial(result);
    }

    public Polynomial Negate() {
        var result = new double[_coefficients.Length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = -_coefficients[index];
        }

        return new Polynomial(result);
    }

    public Polynomial Multiply(Polynomial other) {
        ArgumentNullException.ThrowIfNull(other);
        if (IsZero || other.IsZero) {
            return Zero;
        }

        var result = new double[checked(_coefficients.Length + other._coefficients.Length - 1)];
        for (var left = 0; left < _coefficients.Length; left++) {
            for (var right = 0; right < other._coefficients.Length; right++) {
                result[left + right] += _coefficients[left] * other._coefficients[right];
            }
        }

        return new Polynomial(result);
    }

    public Polynomial Scale(double factor) {
        if (!double.IsFinite(factor)) {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        if (factor == 0 || IsZero) {
            return Zero;
        }

        var result = new double[_coefficients.Length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = _coefficients[index] * factor;
        }

        return new Polynomial(result);
    }

    /// <summary>
    /// Performs polynomial long division and returns quotient and remainder.
    /// </summary>
    public void Divide(
        Polynomial divisor,
        out Polynomial quotient,
        out Polynomial remainder) {
        ArgumentNullException.ThrowIfNull(divisor);
        if (divisor.IsZero) {
            throw new DivideByZeroException(
                "A polynomial cannot be divided by the zero polynomial.");
        }

        if (_coefficients.Length < divisor._coefficients.Length) {
            quotient = Zero;
            remainder = this;
            return;
        }

        var remainderCoefficients = _coefficients.AsSpan().ToArray();
        var quotientCoefficients = new double[
            _coefficients.Length - divisor._coefficients.Length + 1];
        var divisorLeading = divisor.LeadingCoefficient;
        for (var degree = quotientCoefficients.Length - 1; degree >= 0; degree--) {
            var coefficient = remainderCoefficients[
                degree + divisor._coefficients.Length - 1] / divisorLeading;
            quotientCoefficients[degree] = coefficient;
            if (coefficient == 0) {
                continue;
            }

            for (var index = 0; index < divisor._coefficients.Length; index++) {
                remainderCoefficients[degree + index] -=
                    coefficient * divisor._coefficients[index];
            }
        }

        quotient = new Polynomial(quotientCoefficients);
        remainder = new Polynomial(remainderCoefficients);
    }

    public bool Equals(Polynomial? other) {
        return other is not null &&
            _coefficients.AsSpan().SequenceEqual(other._coefficients);
    }

    public override bool Equals(object? obj) {
        return obj is Polynomial other && Equals(other);
    }

    public override int GetHashCode() {
        var hash = new HashCode();
        foreach (var coefficient in _coefficients) {
            hash.Add(coefficient);
        }

        return hash.ToHashCode();
    }

    public override string ToString() {
        return IsZero
            ? "0"
            : string.Join(
                " + ",
                _coefficients.ToArray().Select(
                    static (coefficient, degree) =>
                        degree == 0
                            ? coefficient.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : $"{coefficient.ToString(System.Globalization.CultureInfo.InvariantCulture)}x^{degree}"));
    }

    public static Polynomial operator +(Polynomial left, Polynomial right) {
        ArgumentNullException.ThrowIfNull(left);
        return left.Add(right);
    }

    public static Polynomial operator -(Polynomial left, Polynomial right) {
        ArgumentNullException.ThrowIfNull(left);
        return left.Subtract(right);
    }

    public static Polynomial operator -(Polynomial value) {
        ArgumentNullException.ThrowIfNull(value);
        return value.Negate();
    }

    public static Polynomial operator *(Polynomial left, Polynomial right) {
        ArgumentNullException.ThrowIfNull(left);
        return left.Multiply(right);
    }

    public static Polynomial operator *(double factor, Polynomial value) {
        ArgumentNullException.ThrowIfNull(value);
        return value.Scale(factor);
    }

    public static Polynomial operator *(Polynomial value, double factor) {
        ArgumentNullException.ThrowIfNull(value);
        return value.Scale(factor);
    }
}
