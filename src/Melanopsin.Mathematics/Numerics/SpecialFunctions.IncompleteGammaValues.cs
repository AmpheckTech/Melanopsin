// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the unregularized lower incomplete gamma function
    /// <c>gamma(shape, value)</c>.
    /// </summary>
    /// <remarks>
    /// The result is evaluated from the regularized probability and the gamma
    /// normalizer. It may overflow to positive infinity when the mathematical
    /// result exceeds <see cref="double.MaxValue"/>. The principal real domain
    /// is <c>shape &gt; 0</c> and <c>value &gt;= 0</c>; out-of-domain values
    /// return NaN.
    /// </remarks>
    public static double IncompleteGamma(double shape, double value) {
        if (!(shape > 0) || !double.IsFinite(shape) ||
            double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return 0;
        }

        if (value == double.PositiveInfinity) {
            return Gamma(shape);
        }

        return ScaleByGamma(
            shape,
            IncompleteGammaRegularized(shape, value));
    }

    /// <summary>
    /// Computes the unregularized upper incomplete gamma function
    /// <c>Gamma(shape, value)</c>.
    /// </summary>
    /// <remarks>
    /// The result is evaluated from the complementary regularized probability
    /// and the gamma normalizer. It may overflow to positive infinity when the
    /// mathematical result exceeds <see cref="double.MaxValue"/>. The
    /// principal real domain is <c>shape &gt; 0</c> and
    /// <c>value &gt;= 0</c>; out-of-domain values return NaN.
    /// </remarks>
    public static double IncompleteGammaComplement(
        double shape,
        double value) {
        if (!(shape > 0) || !double.IsFinite(shape) ||
            double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == double.PositiveInfinity) {
            return 0;
        }

        return ScaleByGamma(
            shape,
            IncompleteGammaRegularizedComplement(shape, value));
    }

    /// <summary>
    /// Computes the reciprocal gamma function for every finite real argument.
    /// Integer poles return zero.
    /// </summary>
    public static double ReciprocalGamma(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value <= 0 && value == Math.Truncate(value)) {
            return 0;
        }

        return Math.Exp(-LogGamma(value));
    }

    private static double ScaleByGamma(double shape, double probability) {
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return Gamma(shape);
        }

        var logarithm = LogGamma(shape) + Math.Log(probability);
        return logarithm > Math.Log(double.MaxValue)
            ? double.PositiveInfinity
            : Math.Exp(logarithm);
    }
}
