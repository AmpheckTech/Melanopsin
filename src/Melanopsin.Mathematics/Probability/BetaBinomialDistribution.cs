// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A beta-binomial distribution for a fixed number of trials and a
/// beta-distributed success probability.
/// </summary>
/// <remarks>
/// The distribution is the composition of a beta prior on the success
/// probability with a binomial observation model. It reduces to a discrete
/// uniform distribution when both shapes are one. The cumulative distribution
/// is evaluated by summing probability masses; the support is finite.
/// </remarks>
public readonly record struct BetaBinomialDistribution : IProbabilityDistribution {
    public BetaBinomialDistribution(
        int trialCount,
        double firstShape,
        double secondShape) {
        ArgumentOutOfRangeException.ThrowIfNegative(trialCount);
        DistributionValidation.Scale(firstShape, nameof(firstShape));
        DistributionValidation.Scale(secondShape, nameof(secondShape));
        TrialCount = trialCount;
        FirstShape = firstShape;
        SecondShape = secondShape;
    }

    public int TrialCount { get; }

    public double FirstShape { get; }

    public double SecondShape { get; }

    public double Mean => TrialCount * FirstShape / (FirstShape + SecondShape);

    public double Variance {
        get {
            var shapeSum = FirstShape + SecondShape;
            return TrialCount * FirstShape * SecondShape * (shapeSum + TrialCount) /
                (shapeSum * shapeSum * (shapeSum + 1));
        }
    }

    public double Pdf(double value) {
        var logarithm = LogPmf(value);
        return double.IsNegativeInfinity(logarithm) ? 0 : Math.Exp(logarithm);
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass.
    /// </summary>
    /// <remarks>
    /// Non-integer values, values outside the support, and non-finite values
    /// return negative infinity.
    /// </remarks>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > TrialCount ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        var successes = (int)value;
        return Combinatorics.LogBinomial(TrialCount, successes) +
            SpecialFunctions.LogBeta(
                successes + FirstShape,
                TrialCount - successes + SecondShape) -
            SpecialFunctions.LogBeta(FirstShape, SecondShape);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < 0) {
            return 0;
        }

        if (value >= TrialCount) {
            return 1;
        }

        var last = (int)Math.Floor(value);
        var cumulative = 0.0;
        for (var successes = 0; successes <= last; successes++) {
            cumulative += Pdf(successes);
        }

        return Math.Clamp(cumulative, 0, 1);
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
        var probability = new BetaDistribution(
            FirstShape,
            SecondShape).Sample(ref generator);
        return new BinomialDistribution(
            TrialCount,
            probability).Sample(ref generator);
    }
}
