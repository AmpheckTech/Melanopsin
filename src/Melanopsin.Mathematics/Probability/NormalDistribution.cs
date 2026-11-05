// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A normal distribution parameterized by mean and standard deviation.
/// </summary>
public readonly record struct NormalDistribution : IProbabilityDistribution {
    public NormalDistribution(double mean, double standardDeviation) {
        DistributionValidation.Location(mean, nameof(mean));
        DistributionValidation.Scale(standardDeviation, nameof(standardDeviation));
        Mean = mean;
        StandardDeviation = standardDeviation;
    }

    public double Mean { get; }

    public double StandardDeviation { get; }

    public double Variance => StandardDeviation * StandardDeviation;

    public double Pdf(double value) {
        if (!double.IsFinite(value)) {
            return value == double.PositiveInfinity ||
                value == double.NegativeInfinity
                ? 0
                : double.NaN;
        }

        var standardized = (value - Mean) / StandardDeviation;
        return Constants.InverseSqrt2Pi *
            Math.Exp(-0.5 * standardized * standardized) /
            StandardDeviation;
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        var standardized = (value - Mean) / StandardDeviation;
        return 0.5 * SpecialFunctions.Erfc(-standardized * Constants.InverseSqrt2);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Mean +
            (StandardDeviation * SpecialFunctions.InverseNormalCdf(probability));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Mean + (StandardDeviation *
            DistributionValidation.SampleNormal(ref generator));
    }
}
