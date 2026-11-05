namespace Melanopsin.Mathematics;

/// <summary>
/// An inverse-gamma distribution over strictly positive values.
/// </summary>
/// <remarks>
/// If <c>X</c> has a gamma distribution with shape <c>a</c> and rate
/// <c>b</c>, then <c>1 / X</c> has this inverse-gamma distribution with shape
/// <c>a</c> and scale <c>b</c>. The mean is infinite for shape at most one and
/// the variance is infinite for shape at most two.
/// </remarks>
public readonly record struct InverseGammaDistribution : IProbabilityDistribution {
    public InverseGammaDistribution(double shape, double scale) {
        DistributionValidation.Scale(shape, nameof(shape));
        DistributionValidation.Scale(scale, nameof(scale));
        Shape = shape;
        Scale = scale;
    }

    public double Shape { get; }

    public double Scale { get; }

    public double Mean => Shape > 1
        ? Scale / (Shape - 1)
        : double.PositiveInfinity;

    public double Variance => Shape > 2
        ? Scale * Scale / ((Shape - 1) * (Shape - 1) * (Shape - 2))
        : double.PositiveInfinity;

    public double Pdf(double value) {
        if (double.IsNaN(value) || value < 0) {
            return 0;
        }

        if (value == 0) {
            return 0;
        }

        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the probability density for a
    /// strictly positive value.
    /// </summary>
    /// <remarks>
    /// Values at or below zero and non-finite values return negative infinity.
    /// </remarks>
    public double LogPdf(double value) {
        if (!(value > 0) || !double.IsFinite(value)) {
            return double.NegativeInfinity;
        }

        var logarithm = (Shape * Math.Log(Scale)) -
            SpecialFunctions.LogGamma(Shape) -
            ((Shape + 1) * Math.Log(value)) -
            (Scale / value);
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

        return SpecialFunctions.IncompleteGammaRegularizedComplement(
            Shape,
            Scale / value);
    }

    /// <summary>
    /// Computes the upper-tail probability directly.
    /// </summary>
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

        return SpecialFunctions.IncompleteGammaRegularized(
            Shape,
            Scale / value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Scale / SpecialFunctions.InverseIncompleteGammaRegularized(
            Shape,
            1 - probability);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var gamma = DistributionValidation.SampleStandardGamma(
            ref generator,
            Shape);
        return Scale / gamma;
    }
}
