// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A hypergeometric distribution for successes in sampling without
/// replacement from a finite population.
/// </summary>
/// <remarks>
/// The cumulative distribution is evaluated by summing probability masses.
/// This is deliberate because its support is finite and usually much smaller
/// than the population. Quantiles use an integer binary search over that same
/// definition.
/// </remarks>
public readonly record struct HypergeometricDistribution : IProbabilityDistribution {
    public HypergeometricDistribution(
        int populationSize,
        int successCount,
        int sampleCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(populationSize);
        if (populationSize == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(populationSize),
                "The population size must be positive.");
        }

        if (successCount < 0 || successCount > populationSize) {
            throw new ArgumentOutOfRangeException(nameof(successCount));
        }

        if (sampleCount < 0 || sampleCount > populationSize) {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        PopulationSize = populationSize;
        SuccessCount = successCount;
        SampleCount = sampleCount;
    }

    public int PopulationSize { get; }

    public int SuccessCount { get; }

    public int SampleCount { get; }

    public int Minimum => Math.Max(0, SampleCount - (PopulationSize - SuccessCount));

    public int Maximum => Math.Min(SampleCount, SuccessCount);

    public double Mean => (double)SampleCount * SuccessCount / PopulationSize;

    public double Variance {
        get {
            if (PopulationSize == 1) {
                return 0;
            }

            var successFraction = (double)SuccessCount / PopulationSize;
            return SampleCount *
                successFraction *
                (1 - successFraction) *
                (PopulationSize - SampleCount) /
                (PopulationSize - 1);
        }
    }

    public double Pdf(double value) {
        if (value < Minimum || value > Maximum ||
            value != Math.Truncate(value) ||
            !double.IsFinite(value)) {
            return 0;
        }

        var successes = (int)value;
        var logarithm =
            Combinatorics.LogBinomial(SuccessCount, successes) +
            Combinatorics.LogBinomial(
                PopulationSize - SuccessCount,
                SampleCount - successes) -
            Combinatorics.LogBinomial(PopulationSize, SampleCount);
        return Math.Exp(logarithm);
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at a success
    /// count without first materializing the probability.
    /// </summary>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < Minimum ||
            value > Maximum ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        var successes = (int)value;
        return Combinatorics.LogBinomial(SuccessCount, successes) +
            Combinatorics.LogBinomial(
                PopulationSize - SuccessCount,
                SampleCount - successes) -
            Combinatorics.LogBinomial(PopulationSize, SampleCount);
    }

    public double Cdf(double value) {
        if (value < Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        var last = (int)Math.Floor(value);
        var sum = 0.0;
        for (var successes = Minimum; successes <= last; successes++) {
            sum += Pdf(successes);
        }

        return Math.Min(1, sum);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Minimum;
        }

        if (probability == 1) {
            return Maximum;
        }

        var lower = Minimum;
        var upper = Maximum;
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
        var remainingPopulation = PopulationSize;
        var remainingSuccesses = SuccessCount;
        var remainingSample = SampleCount;
        var successes = 0;
        while (remainingSample > 0) {
            if (generator.NextDouble() <
                (double)remainingSuccesses / remainingPopulation) {
                successes++;
                remainingSuccesses--;
            }

            remainingPopulation--;
            remainingSample--;
        }

        return successes;
    }
}
