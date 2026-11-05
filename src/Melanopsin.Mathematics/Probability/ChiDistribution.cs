// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A chi distribution: the distribution of the positive square root of a
/// central chi-square random variable.
/// </summary>
public readonly record struct ChiDistribution : IProbabilityDistribution {
    public ChiDistribution(double degreesOfFreedom) {
        DistributionValidation.Scale(degreesOfFreedom, nameof(degreesOfFreedom));
        DegreesOfFreedom = degreesOfFreedom;
    }

    public double DegreesOfFreedom { get; }

    public double Mean =>
        Constants.Sqrt2 * Math.Exp(
            SpecialFunctions.LogGamma(0.5 * (DegreesOfFreedom + 1)) -
            SpecialFunctions.LogGamma(0.5 * DegreesOfFreedom));

    public double Variance {
        get {
            var mean = Mean;
            return DegreesOfFreedom - (mean * mean);
        }
    }

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < 0 || double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value == 0) {
            return DegreesOfFreedom < 1
                ? double.PositiveInfinity
                : DegreesOfFreedom == 1
                    ? Constants.Sqrt2 * Constants.InverseSqrtPi
                    : 0;
        }

        var halfDegrees = 0.5 * DegreesOfFreedom;
        var logarithm =
            ((DegreesOfFreedom - 1) * Math.Log(value)) -
            (0.5 * value * value) -
            ((halfDegrees - 1) * Constants.Ln2) -
            SpecialFunctions.LogGamma(halfDegrees);
        return Math.Exp(logarithm);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value <= 0
            ? 0
            : double.IsPositiveInfinity(value)
                ? 1
                : SpecialFunctions.IncompleteGammaRegularized(
                    0.5 * DegreesOfFreedom,
                    0.5 * value * value);
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value <= 0
            ? 1
            : double.IsPositiveInfinity(value)
                ? 0
                : SpecialFunctions.IncompleteGammaRegularizedComplement(
                    0.5 * DegreesOfFreedom,
                    0.5 * value * value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Math.Sqrt(
            2 * SpecialFunctions.InverseIncompleteGammaRegularized(
                0.5 * DegreesOfFreedom,
                probability));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Math.Sqrt(2 * DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * DegreesOfFreedom));
    }
}
