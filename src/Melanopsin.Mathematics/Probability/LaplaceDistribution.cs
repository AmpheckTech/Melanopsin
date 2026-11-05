// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Laplace distribution parameterized by location and scale.
/// </summary>
public readonly record struct LaplaceDistribution : IProbabilityDistribution {
    public LaplaceDistribution(double location, double scale) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        Location = location;
        Scale = scale;
    }

    public double Location { get; }

    public double Scale { get; }

    public double Mean => Location;

    public double Variance => 2 * Scale * Scale;

    public double Pdf(double value) {
        return Math.Exp(-Math.Abs(value - Location) / Scale) / (2 * Scale);
    }

    public double Cdf(double value) {
        var standardized = (value - Location) / Scale;
        return standardized < 0
            ? 0.5 * Math.Exp(standardized)
            : 1 - (0.5 * Math.Exp(-standardized));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return probability < 0.5
            ? Location + (Scale * Math.Log(2 * probability))
            : Location - (Scale * Math.Log(2 * (1 - probability)));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
