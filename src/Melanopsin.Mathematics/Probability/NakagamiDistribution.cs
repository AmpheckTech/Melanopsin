// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Nakagami-m distribution over non-negative values.
/// </summary>
/// <remarks>
/// This is the standard fading-envelope parameterization with shape
/// <c>m &gt;= 1/2</c> and mean power <c>omega &gt; 0</c>. Shape one half is
/// the one-sided half-normal distribution in this parameterization.
/// Quantiles use the inverse regularized incomplete gamma function.
/// </remarks>
public readonly record struct NakagamiDistribution :
    IProbabilityDistribution {
    public NakagamiDistribution(double shape, double spread) {
        if (!double.IsFinite(shape) || shape < 0.5) {
            throw new ArgumentOutOfRangeException(nameof(shape));
        }

        DistributionValidation.Scale(spread, nameof(spread));
        Shape = shape;
        Spread = spread;
    }

    public double Shape { get; }

    /// <summary>
    /// Gets the mean power parameter, often denoted omega.
    /// </summary>
    public double Spread { get; }

    public double Mean {
        get {
            var logarithm = SpecialFunctions.LogGamma(Shape + 0.5) -
                SpecialFunctions.LogGamma(Shape) +
                (0.5 * Math.Log(Spread / Shape));
            return Math.Exp(logarithm);
        }
    }

    public double Variance {
        get {
            var ratio = Math.Exp(
                (2 * SpecialFunctions.LogGamma(Shape + 0.5)) -
                (2 * SpecialFunctions.LogGamma(Shape)) -
                Math.Log(Shape));
            return Spread * (1 - ratio);
        }
    }

    /// <summary>
    /// Gets the positive mode.
    /// </summary>
    public double Mode => Math.Sqrt(
        Spread * (2 * Shape - 1) / (2 * Shape));

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < 0 || double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value == 0) {
            return Shape == 0.5
                ? 2 * Math.Sqrt(Shape / Spread) /
                    SpecialFunctions.Gamma(Shape)
                : 0;
        }

        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the probability density.
    /// </summary>
    /// <remarks>
    /// Negative values and positive infinity return negative infinity. At
    /// zero the result is positive infinity only for shapes below one; the
    /// supported parameter domain starts at one half, where a zero value has
    /// a finite density.
    /// </remarks>
    public double LogPdf(double value) {
        if (!(value >= 0) || !double.IsFinite(value)) {
            return double.NegativeInfinity;
        }

        if (value == 0) {
            return Shape < 1
                ? Math.Log(2) + (Shape * Math.Log(Shape / Spread)) -
                    SpecialFunctions.LogGamma(Shape)
                : double.NegativeInfinity;
        }

        var logarithm = Math.Log(2) +
            (Shape * Math.Log(Shape / Spread)) +
            ((2 * Shape - 1) * Math.Log(value)) -
            SpecialFunctions.LogGamma(Shape) -
            (Shape * value * value / Spread);
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
            Shape,
            Shape * value * value / Spread);
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
            Shape,
            Shape * value * value / Spread);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Math.Sqrt(
            Spread * SpecialFunctions.InverseIncompleteGammaRegularized(
                Shape,
                probability) / Shape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var gamma = DistributionValidation.SampleStandardGamma(
            ref generator,
            Shape);
        return Math.Sqrt(Spread * gamma / Shape);
    }
}
