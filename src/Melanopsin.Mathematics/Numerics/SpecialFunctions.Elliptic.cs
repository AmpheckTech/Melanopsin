// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the complete elliptic integral of the first kind using the
    /// parameter convention <c>K(m) = integral(1/sqrt(1 - m sin^2(t)),
    /// t=0..pi/2)</c>.
    /// </summary>
    /// <remarks>
    /// The real-valued domain is <c>m &lt;= 1</c>. At <c>m = 1</c> the
    /// integral diverges. This parameter convention differs from the modulus
    /// convention <c>K(k)</c>, where <c>m = k^2</c>.
    /// </remarks>
    public static double CompleteEllipticIntegralFirstKind(double parameter) {
        if (double.IsNaN(parameter) || double.IsPositiveInfinity(parameter) ||
            parameter > 1) {
            return double.NaN;
        }

        if (parameter == double.NegativeInfinity) {
            return 0;
        }

        if (parameter == 1) {
            return double.PositiveInfinity;
        }

        var arithmetic = 1.0;
        var geometric = Math.Sqrt(1 - parameter);
        for (var iteration = 0; iteration < MaximumIterations; iteration++) {
            var nextArithmetic = 0.5 * (arithmetic + geometric);
            var nextGeometric = Math.Sqrt(arithmetic) * Math.Sqrt(geometric);
            if (nextArithmetic == arithmetic || nextArithmetic == nextGeometric) {
                arithmetic = nextArithmetic;
                break;
            }

            arithmetic = nextArithmetic;
            geometric = nextGeometric;
        }

        return Constants.PiOver2 / arithmetic;
    }

    /// <summary>
    /// Computes the complete elliptic integral of the second kind using the
    /// parameter convention <c>E(m) = integral(sqrt(1 - m sin^2(t)),
    /// t=0..pi/2)</c>.
    /// </summary>
    /// <remarks>
    /// The real-valued domain is <c>m &lt;= 1</c>. At <c>m = 1</c> the value
    /// is exactly one. This parameter convention differs from the modulus
    /// convention <c>E(k)</c>, where <c>m = k^2</c>.
    /// </remarks>
    public static double CompleteEllipticIntegralSecondKind(double parameter) {
        if (double.IsNaN(parameter) || double.IsPositiveInfinity(parameter) ||
            parameter > 1) {
            return double.NaN;
        }

        if (parameter == 1) {
            return 1;
        }

        if (parameter == double.NegativeInfinity) {
            return double.PositiveInfinity;
        }

        var arithmetic = 1.0;
        var geometric = Math.Sqrt(1 - parameter);
        var correction = 0.5 * parameter;
        var coefficient = 1.0;
        for (var iteration = 0; iteration < MaximumIterations; iteration++) {
            var nextArithmetic = 0.5 * (arithmetic + geometric);
            var nextGeometric = Math.Sqrt(arithmetic) * Math.Sqrt(geometric);
            var current = 0.5 * (arithmetic - geometric);
            correction += coefficient * current * current;
            if (nextArithmetic == arithmetic || nextArithmetic == nextGeometric) {
                arithmetic = nextArithmetic;
                break;
            }

            arithmetic = nextArithmetic;
            geometric = nextGeometric;
            coefficient *= 2;
        }

        var firstKind = Constants.PiOver2 / arithmetic;
        return firstKind * (1 - correction);
    }
}
