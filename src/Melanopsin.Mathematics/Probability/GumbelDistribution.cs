// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A maximum Gumbel distribution, also called the type-I extreme-value
/// distribution.
/// </summary>
public readonly record struct GumbelDistribution : IProbabilityDistribution {
    public GumbelDistribution(double location, double scale) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        Location = location;
        Scale = scale;
    }

    public double Location { get; }

    public double Scale { get; }

    public double Mean => Location + (Constants.EulerMascheroni * Scale);

    public double Variance => Constants.Pi * Constants.Pi * Scale * Scale / 6;

    public double Pdf(double value) {
        if (!double.IsFinite(value)) {
            return 0;
        }

        var standardized = (value - Location) / Scale;
        return Math.Exp(-(standardized + Math.Exp(-standardized))) / Scale;
    }

    public double Cdf(double value) {
        if (double.IsNegativeInfinity(value)) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        var standardized = (value - Location) / Scale;
        return Math.Exp(-Math.Exp(-standardized));
    }

    public double Survival(double value) {
        if (double.IsNegativeInfinity(value)) {
            return 1;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        var standardized = (value - Location) / Scale;
        return -FloatingPointMath.Expm1(-Math.Exp(-standardized));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Location -
            (Scale * Math.Log(-Math.Log(probability)));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
