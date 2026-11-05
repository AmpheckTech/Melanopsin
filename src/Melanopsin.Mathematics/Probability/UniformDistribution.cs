// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A continuous uniform distribution over [Minimum, Maximum].
/// </summary>
public readonly record struct UniformDistribution : IProbabilityDistribution {
    public UniformDistribution(double minimum, double maximum) {
        DistributionValidation.Location(minimum, nameof(minimum));
        DistributionValidation.Location(maximum, nameof(maximum));
        if (maximum <= minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "The maximum must exceed the minimum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Mean => Minimum + ((Maximum - Minimum) * 0.5);

    public double Variance {
        get {
            var width = Maximum - Minimum;
            return width * width / 12;
        }
    }

    public double Pdf(double value) {
        return value >= Minimum && value <= Maximum
            ? 1 / (Maximum - Minimum)
            : 0;
    }

    public double Cdf(double value) {
        if (value <= Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        return (value - Minimum) / (Maximum - Minimum);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return Minimum + ((Maximum - Minimum) * probability);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Minimum + ((Maximum - Minimum) * generator.NextDouble());
    }
}
