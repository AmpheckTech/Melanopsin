// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the exponential integral <c>Ei(x)</c> for a finite real
    /// argument other than zero.
    /// </summary>
    /// <remarks>
    /// For positive arguments this is the Cauchy principal value
    /// <c>PV integral(exp(t)/t, t=-infinity..x)</c>. Negative arguments use
    /// <c>Ei(x) = -E1(-x)</c> for numerical stability.
    /// </remarks>
    public static double ExponentialIntegral(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NegativeInfinity;
        }

        if (value == double.PositiveInfinity) {
            return double.PositiveInfinity;
        }

        if (value == double.NegativeInfinity) {
            return 0;
        }

        if (value < 0) {
            return -ExponentialIntegralE1(-value);
        }

        var result = Constants.EulerMascheroni + Math.Log(value);
        var term = 1.0;
        for (var index = 1; index <= MaximumIterations; index++) {
            term *= value / index;
            var contribution = term / index;
            result += contribution;
            if (!double.IsFinite(result)) {
                return result;
            }

            if (Math.Abs(contribution) <=
                Math.Abs(result) * RelativeAccuracy) {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the exponential integral <c>E1(x)</c> for
    /// <c>x &gt; 0</c>, namely
    /// <c>integral(exp(-t)/t, t=x..infinity)</c>.
    /// </summary>
    public static double ExponentialIntegralE1(double value) {
        if (double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return double.PositiveInfinity;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value > 1) {
            return ContinuedFractionE1(value);
        }

        var result = -Constants.EulerMascheroni - Math.Log(value);
        var term = 1.0;
        for (var index = 1; index <= MaximumIterations; index++) {
            term *= -value / index;
            var contribution = term / index;
            result -= contribution;
            if (Math.Abs(contribution) <=
                Math.Abs(result) * RelativeAccuracy) {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the logarithmic integral <c>li(x)</c> as the principal value
    /// <c>Ei(log(x))</c> for positive finite <paramref name="value"/>.
    /// The value at one is negative infinity.
    /// </summary>
    public static double LogarithmicIntegral(double value) {
        if (!(value > 0) || double.IsPositiveInfinity(value)) {
            return value == double.PositiveInfinity
                ? double.PositiveInfinity
                : double.NaN;
        }

        if (value == 1) {
            return double.NegativeInfinity;
        }

        return ExponentialIntegral(Math.Log(value));
    }

    private static double ContinuedFractionE1(double value) {
        const double Minimum = 1e-300;
        var second = value + 1;
        var third = 1 / Minimum;
        var fourth = 1 / second;
        var result = fourth;
        for (var index = 1; index <= MaximumIterations; index++) {
            var coefficient = -(index * (double)index);
            second += 2;
            fourth = 1 / ((coefficient * fourth) + second);
            third = second + (coefficient / third);
            var delta = fourth * third;
            result *= delta;
            if (Math.Abs(delta - 1) <= RelativeAccuracy) {
                break;
            }
        }

        return Math.Exp(-value) * result;
    }
}
