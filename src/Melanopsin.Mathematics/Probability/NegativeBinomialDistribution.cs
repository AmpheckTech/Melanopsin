// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A negative binomial distribution for the number of failures before a fixed
/// number of successes.
/// </summary>
public readonly record struct NegativeBinomialDistribution : IProbabilityDistribution {
    public NegativeBinomialDistribution(int successCount, double probabilityOfSuccess) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(successCount);
        if (!double.IsFinite(probabilityOfSuccess) ||
            probabilityOfSuccess <= 0 ||
            probabilityOfSuccess > 1) {
            throw new ArgumentOutOfRangeException(nameof(probabilityOfSuccess));
        }

        SuccessCount = successCount;
        ProbabilityOfSuccess = probabilityOfSuccess;
    }

    public int SuccessCount { get; }

    public double ProbabilityOfSuccess { get; }

    public double ProbabilityOfFailure => 1 - ProbabilityOfSuccess;

    public double Mean => SuccessCount * ProbabilityOfFailure /
        ProbabilityOfSuccess;

    public double Variance => SuccessCount * ProbabilityOfFailure /
        (ProbabilityOfSuccess * ProbabilityOfSuccess);

    public double Pdf(double value) {
        if (value < 0 || value != Math.Truncate(value) ||
            !double.IsFinite(value)) {
            return 0;
        }

        if (ProbabilityOfSuccess == 1) {
            return value == 0 ? 1 : 0;
        }

        var logarithm =
            SpecialFunctions.LogGamma(value + SuccessCount) -
            SpecialFunctions.LogGamma(value + 1) -
            SpecialFunctions.LogGamma(SuccessCount) +
            (SuccessCount * Math.Log(ProbabilityOfSuccess)) +
            (value * Math.Log(ProbabilityOfFailure));
        return Math.Exp(logarithm);
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at a failure
    /// count without first materializing the probability.
    /// </summary>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        if (ProbabilityOfSuccess == 1) {
            return value == 0 ? 0 : double.NegativeInfinity;
        }

        return SpecialFunctions.LogGamma(value + SuccessCount) -
            SpecialFunctions.LogGamma(value + 1) -
            SpecialFunctions.LogGamma(SuccessCount) +
            (SuccessCount * Math.Log(ProbabilityOfSuccess)) +
            (value * FloatingPointMath.Log1p(-ProbabilityOfSuccess));
    }

    public double Cdf(double value) {
        if (value < 0) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        return SpecialFunctions.IncompleteBetaRegularized(
            SuccessCount,
            Math.Floor(value) + 1,
            ProbabilityOfSuccess);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1 || ProbabilityOfSuccess == 1) {
            return ProbabilityOfSuccess == 1
                ? 0
                : double.PositiveInfinity;
        }

        var lower = 0.0;
        var upper = Math.Max(
            1,
            Math.Ceiling(Mean + (10 * Math.Sqrt(Variance)) + 10));
        while (Cdf(upper) < probability) {
            lower = upper + 1;
            upper = Math.Ceiling(upper * 2);
            if (!double.IsFinite(upper)) {
                throw new ArithmeticException(
                    "A finite negative-binomial quantile could not be bracketed.");
            }
        }

        while (lower < upper) {
            var midpoint = Math.Floor(lower + ((upper - lower) * 0.5));
            if (Cdf(midpoint) >= probability) {
                upper = midpoint;
            } else {
                lower = midpoint + 1;
            }
        }

        return lower;
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        if (ProbabilityOfSuccess == 1) {
            return 0;
        }

        var rate = SuccessCount * ProbabilityOfFailure /
            ProbabilityOfSuccess;
        var gamma = DistributionValidation.SampleStandardGamma(
            ref generator,
            SuccessCount);
        return new PoissonDistribution(rate * gamma).Sample(ref generator);
    }
}
