// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Finite-value and approximate-comparison helpers for
/// <see cref="Complex"/>. Complex values have no total ordering, so these
/// helpers compare magnitudes rather than individual components.
/// </summary>
public static class ComplexMath {
    public static bool IsFinite(Complex value) {
        return value.IsFinite;
    }

    /// <summary>
    /// Determines whether a finite complex value has magnitude at or below an
    /// absolute tolerance.
    /// </summary>
    public static bool IsNearlyZero(
        Complex value,
        double tolerance = ScalarMath.DefaultTolerance) {
        ValidateTolerance(tolerance);
        return value.IsFinite && value.Magnitude <= tolerance;
    }

    /// <summary>
    /// Compares two complex values using combined absolute and relative
    /// magnitude tolerances.
    /// </summary>
    public static bool IsNearlyEqual(
        Complex left,
        Complex right,
        double absoluteTolerance = ScalarMath.DefaultTolerance,
        double relativeTolerance = ScalarMath.DefaultTolerance) {
        ValidateTolerance(absoluteTolerance);
        ValidateTolerance(relativeTolerance);
        if (!left.IsFinite || !right.IsFinite) {
            return left == right;
        }

        var difference = (left - right).Magnitude;
        var scale = Math.Max(left.Magnitude, right.Magnitude);
        return difference <=
            Math.Max(absoluteTolerance, relativeTolerance * scale);
    }

    /// <summary>
    /// Compares two complex values using an absolute magnitude tolerance.
    /// </summary>
    public static bool ApproximatelyEquals(
        Complex left,
        Complex right,
        double tolerance = ScalarMath.DefaultTolerance) {
        ValidateTolerance(tolerance);
        return IsNearlyZero(left - right, tolerance);
    }


    public static Complex Exp(Complex value) {
        if (value.Real == double.NegativeInfinity) {
            return Complex.Zero;
        }

        if (value.Real == double.PositiveInfinity) {
            if (value.Imaginary == 0 || !double.IsFinite(value.Imaginary)) {
                return new Complex(double.PositiveInfinity, double.NaN);
            }

            return new Complex(
                double.PositiveInfinity * Math.Cos(value.Imaginary),
                double.PositiveInfinity * Math.Sin(value.Imaginary));
        }

        var magnitude = Math.Exp(value.Real);
        return new Complex(
            magnitude * Math.Cos(value.Imaginary),
            magnitude * Math.Sin(value.Imaginary));
    }

    /// <summary>
    /// Computes the principal complex logarithm, including the branch cut on
    /// the negative real axis.
    /// </summary>
    public static Complex Log(Complex value) {
        return new Complex(Math.Log(value.Magnitude), value.Phase);
    }

    public static Complex Log(Complex value, double baseValue) {
        ValidateLogarithmBase(baseValue);
        return Log(value) / Math.Log(baseValue);
    }

    public static Complex Log10(Complex value) {
        return Log(value) / Constants.Ln10;
    }

    public static Complex Log2(Complex value) {
        return Log(value) / Constants.Ln2;
    }

    public static Complex Pow(Complex value, Complex exponent) {
        if (exponent == Complex.Zero) {
            return Complex.One;
        }

        if (value == Complex.Zero) {
            if (exponent.Imaginary == 0 && exponent.Real > 0) {
                return Complex.Zero;
            }

            return Complex.FromDouble(double.NaN);
        }

        return Exp(exponent * Log(value));
    }

    public static Complex Pow(Complex value, double exponent) {
        return Pow(value, new Complex(exponent, 0));
    }

    /// <summary>
    /// Computes the principal square root.
    /// </summary>
    public static Complex Sqrt(Complex value) {
        if (value.Imaginary == 0 && value.Real >= 0) {
            return new Complex(Math.Sqrt(value.Real), value.Imaginary);
        }

        if (!value.IsFinite) {
            return new Complex(double.NaN, double.NaN);
        }

        var magnitude = value.Magnitude;
        var real = Math.Sqrt(0.5 * (magnitude + value.Real));
        var imaginarySign = Math.CopySign(1, value.Imaginary);
        if (value.Imaginary == 0) {
            imaginarySign = 1;
        }

        return new Complex(real, imaginarySign * Math.Sqrt(0.5 * (magnitude - value.Real)));
    }

    public static Complex Sin(Complex value) {
        return new Complex(
            Math.Sin(value.Real) * Math.Cosh(value.Imaginary),
            Math.Cos(value.Real) * Math.Sinh(value.Imaginary));
    }

    public static Complex Cos(Complex value) {
        return new Complex(
            Math.Cos(value.Real) * Math.Cosh(value.Imaginary),
            -Math.Sin(value.Real) * Math.Sinh(value.Imaginary));
    }

    public static Complex Tan(Complex value) {
        return Sin(value) / Cos(value);
    }

    public static Complex Sinh(Complex value) {
        return new Complex(
            Math.Sinh(value.Real) * Math.Cos(value.Imaginary),
            Math.Cosh(value.Real) * Math.Sin(value.Imaginary));
    }

    public static Complex Cosh(Complex value) {
        return new Complex(
            Math.Cosh(value.Real) * Math.Cos(value.Imaginary),
            Math.Sinh(value.Real) * Math.Sin(value.Imaginary));
    }

    public static Complex Tanh(Complex value) {
        return Sinh(value) / Cosh(value);
    }

    public static Complex Asin(Complex value) {
        var imaginaryValue = new Complex(-value.Imaginary, value.Real);
        return new Complex(0, -1) *
            Log(imaginaryValue + Sqrt(Complex.One - (value * value)));
    }

    public static Complex Acos(Complex value) {
        return new Complex(Constants.PiOver2, 0) - Asin(value);
    }

    public static Complex Atan(Complex value) {
        var imaginaryValue = new Complex(-value.Imaginary, value.Real);
        return new Complex(0, 0.5) *
            (Log(Complex.One - imaginaryValue) - Log(Complex.One + imaginaryValue));
    }

    /// <summary>
    /// Computes the principal <paramref name="order"/>th roots of a complex
    /// value and writes them in evenly spaced phase order.
    /// </summary>
    public static bool TryRoots(
        Complex value,
        int order,
        Span<Complex> roots) {
        return TryRoots(value, order, roots, out _);
    }

    /// <summary>
    /// Computes the principal <paramref name="order"/>th roots of a complex
    /// value and reports whether all requested values are finite.
    /// </summary>
    /// <remarks>
    /// A finite zero radicand is not treated as a singularity: every root is
    /// exactly zero and the operation succeeds. Non-finite input clears the
    /// destination. Finite input that overflows or otherwise produces a
    /// non-finite root also clears the destination and reports
    /// <see cref="ComplexRootStatus.NonFiniteOutput"/>.
    /// </remarks>
    public static bool TryRoots(
        Complex value,
        int order,
        Span<Complex> roots,
        out ComplexRootReport report) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);

        if (roots.Length < order) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(roots));
        }

        if (!value.IsFinite) {
            roots[..order].Clear();
            report = new ComplexRootReport(order, ComplexRootStatus.NonFiniteInput);
            return false;
        }

        if (value == Complex.Zero) {
            roots[..order].Clear();
            report = new ComplexRootReport(order, ComplexRootStatus.Succeeded);
            return true;
        }

        var rootMagnitude = Math.Pow(value.Magnitude, 1.0 / order);
        var phase = value.Phase / order;
        var phaseStep = Constants.TwoPi / order;
        for (var index = 0; index < order; index++) {
            var root = Complex.FromPolarCoordinates(
                rootMagnitude,
                phase + (phaseStep * index));
            if (!root.IsFinite) {
                roots[..order].Clear();
                report = new ComplexRootReport(order, ComplexRootStatus.NonFiniteOutput);
                return false;
            }

            roots[index] = root;
        }

        report = new ComplexRootReport(order, ComplexRootStatus.Succeeded);
        return true;
    }

    private static void ValidateTolerance(double tolerance) {
        if (!double.IsFinite(tolerance) || tolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }

    private static void ValidateLogarithmBase(double baseValue) {
        if (!double.IsFinite(baseValue) || baseValue <= 0 || baseValue == 1) {
            throw new ArgumentOutOfRangeException(nameof(baseValue));
        }
    }
}
