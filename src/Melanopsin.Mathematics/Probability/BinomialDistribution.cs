// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A binomial distribution with a fixed non-negative trial count.
/// </summary>
public readonly record struct BinomialDistribution : IProbabilityDistribution {
    public BinomialDistribution(int trialCount, double probabilityOfSuccess) {
        ArgumentOutOfRangeException.ThrowIfNegative(trialCount);
        DistributionValidation.Probability(probabilityOfSuccess);
        TrialCount = trialCount;
        ProbabilityOfSuccess = probabilityOfSuccess;
    }

    public int TrialCount { get; }

    public double ProbabilityOfSuccess { get; }

    public double ProbabilityOfFailure => 1 - ProbabilityOfSuccess;

    public double Mean => TrialCount * ProbabilityOfSuccess;

    public double Variance => TrialCount *
        ProbabilityOfSuccess *
        ProbabilityOfFailure;

    public double Pdf(double value) {
        if (value < 0 || value > TrialCount ||
            value != Math.Truncate(value)) {
            return 0;
        }

        var successes = (int)value;
        if (ProbabilityOfSuccess == 0) {
            return successes == 0 ? 1 : 0;
        }

        if (ProbabilityOfSuccess == 1) {
            return successes == TrialCount ? 1 : 0;
        }

        var logarithm = SpecialFunctions.LogGamma(TrialCount + 1) -
            SpecialFunctions.LogGamma(successes + 1) -
            SpecialFunctions.LogGamma(TrialCount - successes + 1) +
            (successes * Math.Log(ProbabilityOfSuccess)) +
            ((TrialCount - successes) * Math.Log(ProbabilityOfFailure));
        return Math.Exp(logarithm);
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at a success
    /// count without first materializing the probability.
    /// </summary>
    /// <remarks>
    /// Non-integer values, values outside the support, and zero-probability
    /// outcomes return negative infinity.
    /// </remarks>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > TrialCount ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        var successes = (int)value;
        if (ProbabilityOfSuccess == 0) {
            return successes == 0 ? 0 : double.NegativeInfinity;
        }

        if (ProbabilityOfSuccess == 1) {
            return successes == TrialCount ? 0 : double.NegativeInfinity;
        }

        return Combinatorics.LogBinomial(TrialCount, successes) +
            (successes * Math.Log(ProbabilityOfSuccess)) +
            ((TrialCount - successes) * Math.Log(ProbabilityOfFailure));
    }

    public double Cdf(double value) {
        if (value < 0) {
            return 0;
        }

        if (value >= TrialCount) {
            return 1;
        }

        var successes = Math.Floor(value);
        if (ProbabilityOfSuccess == 0) {
            return 1;
        }

        if (ProbabilityOfSuccess == 1) {
            return 0;
        }

        return SpecialFunctions.IncompleteBetaRegularized(
            TrialCount - successes,
            successes + 1,
            ProbabilityOfFailure);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0 || TrialCount == 0) {
            return 0;
        }

        if (probability == 1) {
            return TrialCount;
        }

        var lower = 0;
        var upper = TrialCount;
        while (lower < upper) {
            var midpoint = lower + ((upper - lower) / 2);
            if (Cdf(midpoint) >= probability) {
                upper = midpoint;
            } else {
                lower = midpoint + 1;
            }
        }

        return lower;
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
