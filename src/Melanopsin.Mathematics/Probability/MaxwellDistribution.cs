// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Maxwell-Boltzmann distribution for the magnitude of a three-dimensional
/// isotropic Gaussian vector.
/// </summary>
/// <remarks>
/// The scale parameter is the standard deviation of each Cartesian component.
/// The distribution is the chi distribution with three degrees of freedom
/// scaled by <see cref="Scale"/>.
/// </remarks>
public readonly record struct MaxwellDistribution : IProbabilityDistribution {
    public MaxwellDistribution(double scale) {
        DistributionValidation.Scale(scale, nameof(scale));
        Scale = scale;
    }

    public double Scale { get; }

    public double Mean => Scale * Math.Sqrt(8 / Constants.Pi);

    public double Variance =>
        Scale * Scale * (3 - (8 / Constants.Pi));

    /// <summary>
    /// Gets the positive mode.
    /// </summary>
    public double Mode => Constants.Sqrt2 * Scale;

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (!(value > 0) || double.IsPositiveInfinity(value)) {
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

        var logarithm = 0.5 * Math.Log(2 / Constants.Pi) +
            (2 * Math.Log(value)) -
            (3 * Math.Log(Scale)) -
            (value * value / (2 * Scale * Scale));
        return double.IsFinite(logarithm)
            ? logarithm
            : double.NegativeInfinity;
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

        return SpecialFunctions.IncompleteGammaRegularized(
            1.5,
            value * value / (2 * Scale * Scale));
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= 0) {
            return 1;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        return SpecialFunctions.IncompleteGammaRegularizedComplement(
            1.5,
            value * value / (2 * Scale * Scale));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Scale * Math.Sqrt(
            2 * SpecialFunctions.InverseIncompleteGammaRegularized(1.5, probability));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var squared = 2 * DistributionValidation.SampleStandardGamma(
            ref generator,
            1.5);
        return Scale * Math.Sqrt(squared);
    }
}
