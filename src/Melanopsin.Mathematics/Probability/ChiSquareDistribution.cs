// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A central chi-square distribution.
/// </summary>
public readonly record struct ChiSquareDistribution : IProbabilityDistribution {
    public ChiSquareDistribution(double degreesOfFreedom) {
        DistributionValidation.Scale(degreesOfFreedom, nameof(degreesOfFreedom));
        DegreesOfFreedom = degreesOfFreedom;
    }

    public double DegreesOfFreedom { get; }

    public double Mean => DegreesOfFreedom;

    public double Variance => 2 * DegreesOfFreedom;

    public double Pdf(double value) {
        if (double.IsNaN(value) || value < 0 || double.IsPositiveInfinity(value)) {
            return 0;
        }

        var halfDegrees = 0.5 * DegreesOfFreedom;
        if (value == 0) {
            return halfDegrees < 1
                ? double.PositiveInfinity
                : halfDegrees == 1 ? 0.5 : 0;
        }

        var logarithm =
            ((halfDegrees - 1) * Math.Log(value)) -
            (0.5 * value) -
            (halfDegrees * Constants.Ln2) -
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
                    0.5 * value);
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
                    0.5 * value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return 2 * SpecialFunctions.InverseIncompleteGammaRegularized(
            0.5 * DegreesOfFreedom,
            probability);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return 2 * DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * DegreesOfFreedom);
    }
}
