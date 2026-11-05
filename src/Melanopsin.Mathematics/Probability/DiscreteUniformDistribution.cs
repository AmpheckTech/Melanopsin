// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A discrete uniform distribution over every integer in an inclusive range.
/// </summary>
public readonly record struct DiscreteUniformDistribution : IProbabilityDistribution {
    public DiscreteUniformDistribution(int minimum, int maximum) {
        if (maximum < minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "The maximum must not be less than the minimum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public int Minimum { get; }

    public int Maximum { get; }

    private double Count => (double)Maximum - Minimum + 1;

    public double Mean => ((double)Minimum + Maximum) * 0.5;

    public double Variance => ((Count * Count) - 1) / 12;

    public double Pdf(double value) {
        return value >= Minimum &&
            value <= Maximum &&
            value == Math.Truncate(value)
            ? 1 / Count
            : 0;
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at an integer.
    /// </summary>
    public double LogPmf(double value) {
        return value >= Minimum &&
            value <= Maximum &&
            value == Math.Truncate(value)
            ? -Math.Log(Count)
            : double.NegativeInfinity;
    }

    public double Cdf(double value) {
        if (value < Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        return (Math.Floor(value) - Minimum + 1) / Count;
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Minimum;
        }

        return Math.Min(
            Maximum,
            Minimum + Math.Floor(probability * Count));
    }

    public int Sample(ref Xoshiro256StarStar generator) {
        var offset = generator.NextInt64(checked((long)Count));
        return Minimum + (int)offset;
    }
}
