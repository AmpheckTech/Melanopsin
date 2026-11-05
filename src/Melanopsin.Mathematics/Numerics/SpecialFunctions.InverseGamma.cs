// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the inverse of the regularized lower incomplete gamma function
    /// with respect to its second argument.
    /// </summary>
    /// <param name="shape">The finite positive gamma shape parameter.</param>
    /// <param name="probability">A probability in the closed interval [0, 1].</param>
    /// <returns>
    /// The unique non-negative <c>value</c> satisfying
    /// <c>P(shape, value) = probability</c>. The endpoints map to zero and
    /// positive infinity.
    /// </returns>
    /// <remarks>
    /// The implementation brackets the root monotonically and applies a
    /// safeguarded Newton iteration with bisection fallback. For upper-tail
    /// probabilities it solves the complementary function directly, avoiding
    /// cancellation in <c>1 - P</c>. The stopping rule is relative to the
    /// magnitude of the current bracket; no absolute function-value threshold
    /// is used. The algorithm allocates no memory.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="shape"/> is not finite and positive, or
    /// <paramref name="probability"/> is outside [0, 1].
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The root cannot be represented with a finite double-precision bracket.
    /// </exception>
    public static double InverseIncompleteGammaRegularized(
        double shape,
        double probability) {
        if (!(shape > 0) || !double.IsFinite(shape)) {
            throw new ArgumentOutOfRangeException(nameof(shape));
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
            return double.PositiveInfinity;
        }

        var useLowerTail = probability <= 0.5;
        var target = useLowerTail ? probability : 1 - probability;
        var lower = 0.0;
        var upper = Math.Max(1, shape);
        var upperValue = EvaluateIncompleteGammaTail(
            shape,
            upper,
            useLowerTail);
        for (var iteration = 0;
            iteration < 2048 && !HasBracketedRoot(
                upperValue,
                target,
                useLowerTail);
            iteration++) {
            lower = upper;
            if (upper > double.MaxValue * 0.5) {
                upper = double.MaxValue;
            } else {
                upper *= 2;
            }

            if (!double.IsFinite(upper) || upper <= lower) {
                throw new ArithmeticException(
                    "A finite incomplete-gamma inverse could not be bracketed.");
            }

            upperValue = EvaluateIncompleteGammaTail(
                shape,
                upper,
                useLowerTail);
        }

        if (!HasBracketedRoot(upperValue, target, useLowerTail)) {
            throw new ArithmeticException(
                "A finite incomplete-gamma inverse could not be bracketed.");
        }

        var estimate = InitialEstimate(shape, probability);
        if (!(estimate > lower && estimate < upper) ||
            !double.IsFinite(estimate)) {
            estimate = lower + ((upper - lower) * 0.5);
        }

        for (var iteration = 0; iteration < 256; iteration++) {
            var value = EvaluateIncompleteGammaTail(
                shape,
                estimate,
                useLowerTail);
            var difference = value - target;
            if (useLowerTail) {
                if (difference < 0) {
                    lower = estimate;
                } else {
                    upper = estimate;
                }
            } else if (difference > 0) {
                lower = estimate;
            } else {
                upper = estimate;
            }

            var midpoint = lower + ((upper - lower) * 0.5);
            if (midpoint == lower ||
                midpoint == upper ||
                upper - lower <=
                    64 * ScalarMath.MachineEpsilon *
                    Math.Max(Math.Abs(lower), Math.Abs(upper))) {
                return midpoint;
            }

            var logarithm = ((shape - 1) * Math.Log(estimate)) -
                estimate -
                LogGamma(shape);
            var density = double.IsFinite(logarithm)
                ? Math.Exp(logarithm)
                : 0;
            var next = midpoint;
            if (density > 0 && double.IsFinite(density)) {
                next = useLowerTail
                    ? estimate - (difference / density)
                    : estimate + (difference / density);
                if (!(next > lower && next < upper) ||
                    !double.IsFinite(next)) {
                    next = midpoint;
                }
            }

            if (next == estimate) {
                return next;
            }

            estimate = next;
        }

        return lower + ((upper - lower) * 0.5);
    }

    private static bool HasBracketedRoot(
        double value,
        double target,
        bool useLowerTail) {
        return useLowerTail ? value >= target : value <= target;
    }

    private static double EvaluateIncompleteGammaTail(
        double shape,
        double value,
        bool useLowerTail) {
        return useLowerTail
            ? IncompleteGammaRegularized(shape, value)
            : IncompleteGammaRegularizedComplement(shape, value);
    }

    private static double InitialEstimate(double shape, double probability) {
        if (shape <= 1) {
            var logarithm = Math.Log(probability) +
                LogGamma(shape + 1);
            return Math.Exp(logarithm / shape);
        }

        var standardized = InverseNormalCdf(probability);
        var correction = 1 -
            (1 / (9 * shape)) +
            (standardized / (3 * Math.Sqrt(shape)));
        if (correction > 0) {
            var cube = correction * correction * correction;
            var approximation = shape * cube;
            if (double.IsFinite(approximation) && approximation > 0) {
                return approximation;
            }
        }

        var asymptoticLogarithm = Math.Log(probability) +
            LogGamma(shape + 1);
        return Math.Exp(asymptoticLogarithm / shape);
    }
}
