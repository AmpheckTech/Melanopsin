// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Poisson distribution with a strictly positive rate.
/// </summary>
public readonly record struct PoissonDistribution : IProbabilityDistribution {
    public PoissonDistribution(double rate) {
        DistributionValidation.Scale(rate, nameof(rate));
        Rate = rate;
    }

    public double Rate { get; }

    public double Mean => Rate;

    public double Variance => Rate;

    public double Pdf(double value) {
        if (value < 0 || value != Math.Truncate(value) ||
            !double.IsFinite(value)) {
            return 0;
        }

        return Math.Exp(
            (-Rate) + (value * Math.Log(Rate)) -
            SpecialFunctions.LogGamma(value + 1));
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at an integer
    /// count without first materializing the probability.
    /// </summary>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        return (-Rate) +
            (value * Math.Log(Rate)) -
            SpecialFunctions.LogGamma(value + 1);
    }

    public double Cdf(double value) {
        if (value < 0) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        var maximum = Math.Floor(value);
        return SpecialFunctions.IncompleteGammaRegularizedComplement(
            maximum + 1,
            Rate);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        var lower = 0.0;
        var upper = Math.Max(
            1,
            Math.Floor(Rate + (10 * Math.Sqrt(Rate)) + 10));
        while (Cdf(upper) < probability) {
            lower = upper + 1;
            upper = Math.Ceiling(upper * 2);
            if (!double.IsFinite(upper)) {
                throw new ArithmeticException(
                    "A finite Poisson quantile could not be bracketed.");
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
        return Quantile(generator.NextDouble());
    }
}
