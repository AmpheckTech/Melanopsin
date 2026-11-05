// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An inverse-Gaussian distribution over strictly positive values.
/// </summary>
/// <remarks>
/// The constructor parameters <c>mean</c> and <c>shape</c> must both be finite
/// and positive. The distribution is also known as the Wald distribution. Its cumulative
/// distribution function is evaluated with the stable tail form of the
/// standard normal distribution. Quantiles use scale-relative monotone
/// bracketing; the upper endpoint at probability one is positive infinity.
/// </remarks>
public readonly record struct InverseGaussianDistribution : IProbabilityDistribution {
    public InverseGaussianDistribution(double mean, double shape) {
        DistributionValidation.Scale(mean, nameof(mean));
        DistributionValidation.Scale(shape, nameof(shape));
        Mean = mean;
        Shape = shape;
    }

    public double Mean { get; }

    public double Shape { get; }

    public double Variance => Mean * Mean * Mean / Shape;

    public double Pdf(double value) {
        if (double.IsNaN(value) || value <= 0 || double.IsPositiveInfinity(value)) {
            return 0;
        }

        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the probability density.
    /// </summary>
    /// <remarks>
    /// Values at or below zero and positive infinity return negative infinity.
    /// </remarks>
    public double LogPdf(double value) {
        if (!(value > 0) || !double.IsFinite(value)) {
            return double.NegativeInfinity;
        }

        var difference = value - Mean;
        return (0.5 * (Math.Log(Shape) - Math.Log(Constants.TwoPi) -
            (3 * Math.Log(value)))) -
            (Shape * difference * difference / (2 * Mean * Mean * value));
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= 0) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        var root = Math.Sqrt(Shape / value);
        var first = NormalCdf(root * ((value / Mean) - 1));
        var secondArgument = -root * ((value / Mean) + 1);
        var logSecond = (2 * Shape / Mean) + LogNormalCdf(secondArgument);
        var second = double.IsNegativeInfinity(logSecond)
            ? 0
            : Math.Exp(logSecond);
        return Math.Clamp(first + second, 0, 1);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return DistributionValidation.FindQuantileOnPositiveSupport(
            Cdf,
            probability,
            Mean);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var normal = DistributionValidation.SampleNormal(ref generator);
        var squared = normal * normal;
        var meanSquared = Mean * Mean;
        var candidate = Mean + (meanSquared * squared / (2 * Shape)) -
            (Mean / (2 * Shape)) *
            Math.Sqrt((4 * Mean * Shape * squared) + (meanSquared * squared * squared));
        var acceptance = Mean / (Mean + candidate);
        return generator.NextDouble() <= acceptance
            ? candidate
            : meanSquared / candidate;
    }

    private static double NormalCdf(double value) {
        return 0.5 * SpecialFunctions.Erfc(
            -value * Constants.InverseSqrt2);
    }

    private static double LogNormalCdf(double value) {
        if (value >= 0) {
            var probability = NormalCdf(value);
            return probability > 0 ? Math.Log(probability) : double.NegativeInfinity;
        }

        var ratio = -value * Constants.InverseSqrt2;
        return -Math.Log(2) - (ratio * ratio) +
            Math.Log(SpecialFunctions.Erfcx(ratio));
    }
}
