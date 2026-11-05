// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Stable elementary functions that complement the <see cref="Math"/> and
/// <see cref="MathF"/> APIs.
/// </summary>
/// <remarks>
/// These functions avoid avoidable overflow and cancellation near domain
/// boundaries. Angles are in radians. Unless stated otherwise, NaN inputs
/// produce NaN and an out-of-domain finite input produces NaN.
/// </remarks>
public static class ElementaryFunctions {
    /// <summary>
    /// Computes <c>sqrt(x^2 + y^2)</c> without avoidable intermediate overflow
    /// or underflow.
    /// </summary>
    public static double Hypot(double x, double y) {
        return FloatingPointMath.Hypot(x, y);
    }

    /// <summary>
    /// Computes <c>sqrt(x^2 + y^2 + z^2)</c> with a common scale so that
    /// intermediate squares do not overflow or underflow.
    /// </summary>
    public static double Hypot(double x, double y, double z) {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) {
            return double.NaN;
        }

        if (double.IsInfinity(x) ||
            double.IsInfinity(y) ||
            double.IsInfinity(z)) {
            return double.PositiveInfinity;
        }

        var scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (scale == 0) {
            return 0;
        }

        var scaledX = x / scale;
        var scaledY = y / scale;
        var scaledZ = z / scale;
        return scale * Math.Sqrt(
            (scaledX * scaledX) + (scaledY * scaledY) + (scaledZ * scaledZ));
    }

    /// <summary>
    /// Computes <c>exp(value) - 1</c> with reduced cancellation near zero.
    /// </summary>
    public static double Expm1(double value) {
        return FloatingPointMath.Expm1(value);
    }

    /// <summary>
    /// Computes <c>log(1 + value)</c> with reduced cancellation near zero.
    /// The domain is <c>value &gt;= -1</c>.
    /// </summary>
    public static double Log1p(double value) {
        return FloatingPointMath.Log1p(value);
    }

    /// <summary>
    /// Computes <c>log(1 - exp(-value))</c> accurately for a non-negative
    /// <paramref name="value"/>. The domain is <c>value &gt;= 0</c>; negative
    /// inputs and NaN return NaN.
    /// </summary>
    public static double Log1mexp(double value) {
        if (double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NegativeInfinity;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        return value <= Constants.Ln2
            ? Math.Log(-FloatingPointMath.Expm1(-value))
            : FloatingPointMath.Log1p(-Math.Exp(-value));
    }

    /// <summary>
    /// Computes the logistic function <c>1 / (1 + exp(-value))</c> without
    /// overflowing the exponential for large negative inputs.
    /// </summary>
    public static double Logistic(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value >= 0) {
            return 1 / (1 + Math.Exp(-value));
        }

        var exponential = Math.Exp(value);
        return exponential / (1 + exponential);
    }

    /// <summary>
    /// Computes the inverse of <see cref="Logistic"/> on [0, 1]:
    /// <c>log(p / (1 - p))</c>. The endpoints map to signed infinity and
    /// values outside [0, 1] return NaN.
    /// </summary>
    public static double Logit(double probability) {
        if (double.IsNaN(probability) || probability < 0 || probability > 1) {
            return double.NaN;
        }

        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Math.Log(probability) - FloatingPointMath.Log1p(-probability);
    }

    /// <summary>
    /// Computes <c>log(1 + exp(value))</c> without intermediate overflow.
    /// </summary>
    public static double Softplus(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return Math.Max(value, 0) +
            FloatingPointMath.Log1p(Math.Exp(-Math.Abs(value)));
    }

    /// <summary>
    /// Computes <c>log(exp(x) + exp(y))</c> without avoidable overflow.
    /// If both arguments are negative infinity the result is negative
    /// infinity; positive infinity dominates.
    /// </summary>
    public static double LogAddExp(double x, double y) {
        if (double.IsNaN(x) || double.IsNaN(y)) {
            return double.NaN;
        }

        if (x == double.PositiveInfinity || y == double.PositiveInfinity) {
            return double.PositiveInfinity;
        }

        if (x == double.NegativeInfinity && y == double.NegativeInfinity) {
            return double.NegativeInfinity;
        }

        var maximum = Math.Max(x, y);
        return maximum +
            FloatingPointMath.Log1p(Math.Exp(-Math.Abs(x - y)));
    }

    /// <summary>
    /// Computes the unnormalized sinc function <c>sin(value) / value</c> in
    /// radians, with a removable value of one at zero.
    /// </summary>
    public static double Sinc(double value) {
        return value == 0 ? 1 : Math.Sin(value) / value;
    }
}
