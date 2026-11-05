// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private const int InverseBetaMaximumIterations = 512;

    /// <summary>
    /// Inverts the regularized incomplete beta function with respect to
    /// <c>x</c>.
    /// </summary>
    /// <param name="firstShape">A finite positive first shape parameter.</param>
    /// <param name="secondShape">A finite positive second shape parameter.</param>
    /// <param name="probability">A probability in the closed interval [0, 1].</param>
    /// <remarks>
    /// The upper half of the probability range is evaluated through the beta
    /// symmetry relation with the complementary lower tail. This avoids
    /// subtracting a nearly unit value from one. The lower-tail solver starts
    /// from the first-order endpoint asymptote and then brackets and bisects in
    /// ordinary <c>x</c>; no absolute probability threshold is used as a
    /// singularity test.
    /// </remarks>
    public static double InverseIncompleteBetaRegularized(
        double firstShape,
        double secondShape,
        double probability) {
        if (!(firstShape > 0) || !double.IsFinite(firstShape)) {
            throw new ArgumentOutOfRangeException(nameof(firstShape));
        }

        if (!(secondShape > 0) || !double.IsFinite(secondShape)) {
            throw new ArgumentOutOfRangeException(nameof(secondShape));
        }

        if (!double.IsFinite(probability) ||
            probability < 0 ||
            probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return 1;
        }

        if (probability <= 0.5) {
            return InverseLowerBeta(
                firstShape,
                secondShape,
                probability);
        }

        // I_x(a, b) = 1 - I_(1-x)(b, a). Solving the smaller complementary
        // tail first keeps the supplied probability away from one.
        var complement = 1 - probability;
        var reflected = InverseLowerBeta(
            secondShape,
            firstShape,
            complement);
        return 1 - reflected;
    }

    private static double InverseLowerBeta(
        double firstShape,
        double secondShape,
        double probability) {
        var logBeta = LogBeta(firstShape, secondShape);
        var estimate = Math.Exp(
            (Math.Log(probability) + Math.Log(firstShape) + logBeta) /
            firstShape);
        if (!(estimate > 0) || !double.IsFinite(estimate)) {
            estimate = Math.ScaleB(1, -1022);
        }

        estimate = Math.Min(estimate, 1);
        var lower = 0.0;
        var upper = 1.0;
        if (IncompleteBetaRegularized(firstShape, secondShape, estimate) >= probability) {
            upper = estimate;
        } else {
            lower = estimate;
            var candidate = Math.Min(1, estimate * 2);
            while (IncompleteBetaRegularized(
                       firstShape,
                       secondShape,
                       candidate) < probability) {
                lower = candidate;
                if (candidate == 1) {
                    break;
                }

                candidate = Math.Min(1, candidate * 2);
                if (candidate == lower) {
                    candidate = Math.BitIncrement(lower);
                }
            }

            upper = candidate;
        }

        var previousMidpoint = double.NaN;
        for (var iteration = 0;
             iteration < InverseBetaMaximumIterations;
             iteration++) {
            double midpoint;
            if (lower == 0) {
                midpoint = upper * 0.5;
            } else if (upper == 1) {
                midpoint = lower + ((1 - lower) * 0.5);
            } else {
                // Geometric bisection is preferable when the bracket spans
                // several orders of magnitude near an endpoint.
                midpoint = Math.Sqrt(lower) * Math.Sqrt(upper);
            }

            if (!(midpoint > lower && midpoint < upper) ||
                midpoint == previousMidpoint) {
                return midpoint;
            }

            previousMidpoint = midpoint;
            if (IncompleteBetaRegularized(
                    firstShape,
                    secondShape,
                    midpoint) < probability) {
                lower = midpoint;
            } else {
                upper = midpoint;
            }
        }

        return lower + ((upper - lower) * 0.5);
    }
}
