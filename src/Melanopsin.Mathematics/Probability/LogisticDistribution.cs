// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A logistic distribution parameterized by location and scale.
/// </summary>
public readonly record struct LogisticDistribution : IProbabilityDistribution {
    public LogisticDistribution(double location, double scale) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        Location = location;
        Scale = scale;
    }

    public double Location { get; }

    public double Scale { get; }

    public double Mean => Location;

    public double Variance => Constants.Pi * Constants.Pi * Scale * Scale / 3;

    public double Pdf(double value) {
        var standardized = (value - Location) / Scale;
        var exponential = Math.Exp(-Math.Abs(standardized));
        var denominator = 1 + exponential;
        return exponential / (Scale * denominator * denominator);
    }

    public double Cdf(double value) {
        var standardized = (value - Location) / Scale;
        if (standardized >= 0) {
            return 1 / (1 + Math.Exp(-standardized));
        }

        var exponential = Math.Exp(standardized);
        return exponential / (1 + exponential);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Location + (Scale * Math.Log(probability / (1 - probability)));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
