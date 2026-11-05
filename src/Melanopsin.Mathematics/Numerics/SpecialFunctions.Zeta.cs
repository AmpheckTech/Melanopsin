// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private static readonly double[] ZetaBernoulliNumbers = [
        1.0 / 6.0,
        -1.0 / 30.0,
        1.0 / 42.0,
        -1.0 / 30.0,
        5.0 / 66.0,
        -691.0 / 2730.0,
        7.0 / 6.0,
    ];

    /// <summary>
    /// Computes the Riemann zeta function for real arguments.
    /// </summary>
    /// <remarks>
    /// The simple pole at one has no signed limit; this method returns NaN
    /// there. Positive arguments use Euler-Maclaurin summation and negative
    /// arguments use the Riemann functional equation.
    /// </remarks>
    public static double RiemannZeta(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 1) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        if (double.IsNegativeInfinity(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return -0.5;
        }

        if (value < 0 && value == Math.Truncate(value) && (value % 2 == 0)) {
            return 0;
        }

        if (value < 0) {
            return Math.Pow(2, value) *
                Math.Pow(Constants.Pi, value - 1) *
                Math.Sin(Constants.Pi * value / 2) *
                Gamma(1 - value) *
                RiemannZeta(1 - value);
        }

        var count = value < 2 ? 32 : 16;
        var sum = 0.0;
        for (var index = 1; index < count; index++) {
            sum += Math.Pow(index, -value);
        }

        var countPower = Math.Pow(count, -value);
        sum += Math.Pow(count, 1 - value) / (value - 1);
        sum += 0.5 * countPower;
        var rising = value;
        var countFactor = countPower / count;
        for (var index = 0; index < ZetaBernoulliNumbers.Length; index++) {
            var order = 2 * index + 2;
            if (index > 0) {
                rising *= (value + order - 3) * (value + order - 2);
            }

            var term = ZetaBernoulliNumbers[index] / Combinatorics.Factorial(order) *
                rising * countFactor;
            sum += term;
            countFactor /= count * count;
            if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                break;
            }
        }

        return sum;
    }
}
