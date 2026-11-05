// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Rayleigh distribution on non-negative values.
/// </summary>
/// <remarks>
/// The distribution models the magnitude of a two-dimensional isotropic
/// Gaussian vector. The scale parameter is the Gaussian component standard
/// deviation, so both the mean and variance depend on it.
/// </remarks>
public readonly record struct RayleighDistribution : IProbabilityDistribution {
    public RayleighDistribution(double scale) {
        DistributionValidation.Scale(scale, nameof(scale));
        Scale = scale;
    }

    public double Scale { get; }

    public double Mean => Scale * Constants.SqrtPiOver2;

    public double Variance => (2 - Constants.PiOver2) * Scale * Scale;

    public double Pdf(double value) {
        if (value < 0 || !double.IsFinite(value)) {
            return 0;
        }

        var ratio = value / Scale;
        return ratio == 0
            ? 0
            : (ratio / Scale) * Math.Exp(-0.5 * ratio * ratio);
    }

    public double Cdf(double value) {
        if (value <= 0) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        var ratio = value / Scale;
        return -FloatingPointMath.Expm1(-0.5 * ratio * ratio);
    }

    public double Survival(double value) {
        if (value <= 0) {
            return 1;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        var ratio = value / Scale;
        return Math.Exp(-0.5 * ratio * ratio);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Scale * Math.Sqrt(-2 * FloatingPointMath.Log1p(-probability));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Scale * Math.Sqrt(-2 * Math.Log(1 - generator.NextDouble()));
    }
}
