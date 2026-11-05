// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-parameter Weibull distribution on non-negative values.
/// </summary>
/// <remarks>
/// The distribution was introduced by
/// <see href="https://doi.org/10.1115/1.4010337">Weibull (1951)</see>.
/// </remarks>
public readonly record struct WeibullDistribution : IProbabilityDistribution {
    public WeibullDistribution(double shape, double scale) {
        DistributionValidation.Scale(shape, nameof(shape));
        DistributionValidation.Scale(scale, nameof(scale));
        Shape = shape;
        Scale = scale;
    }

    public double Shape { get; }

    public double Scale { get; }

    public double Mean => Scale * SpecialFunctions.Gamma(1 + (1 / Shape));

    public double Variance {
        get {
            var same = SpecialFunctions.Gamma(1 + (1 / Shape));
            var doubled = SpecialFunctions.Gamma(1 + (2 / Shape));
            return Scale * Scale * (doubled - (same * same));
        }
    }

    public double Pdf(double value) {
        if (value < 0 || !double.IsFinite(value)) {
            return 0;
        }

        if (value == 0) {
            return Shape < 1
                ? double.PositiveInfinity
                : Shape == 1 ? 1 / Scale : 0;
        }

        var ratio = value / Scale;
        return (Shape / Scale) *
            Math.Pow(ratio, Shape - 1) *
            Math.Exp(-Math.Pow(ratio, Shape));
    }

    public double Cdf(double value) {
        return value <= 0
            ? 0
            : -FloatingPointMath.Expm1(-Math.Pow(value / Scale, Shape));
    }

    public double Survival(double value) {
        return value <= 0
            ? 1
            : Math.Exp(-Math.Pow(value / Scale, Shape));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return probability == 1
            ? double.PositiveInfinity
            : Scale * Math.Pow(-FloatingPointMath.Log1p(-probability), 1 / Shape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
