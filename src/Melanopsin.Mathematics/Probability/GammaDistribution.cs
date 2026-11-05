namespace Melanopsin.Mathematics;

/// <summary>
/// A gamma distribution parameterized by shape and rate.
/// </summary>
/// <remarks>
/// Rate is the reciprocal of the scale. The mean is
/// <c>shape / rate</c> and the variance is <c>shape / rate^2</c>.
/// </remarks>
public readonly record struct GammaDistribution : IProbabilityDistribution {
    public GammaDistribution(double shape, double rate) {
        DistributionValidation.Scale(shape, nameof(shape));
        DistributionValidation.Scale(rate, nameof(rate));
        Shape = shape;
        Rate = rate;
    }

    public double Shape { get; }

    public double Rate { get; }

    public double Scale => 1 / Rate;

    public double Mean => Shape / Rate;

    public double Variance => Shape / (Rate * Rate);

    public double Pdf(double value) {
        if (value < 0 || !double.IsFinite(value)) {
            return 0;
        }

        if (value == 0) {
            if (Shape < 1) {
                return double.PositiveInfinity;
            }

            return Shape == 1 ? Rate : 0;
        }

        return Math.Exp(
            ((Shape - 1) * Math.Log(value)) -
            (Rate * value) -
            SpecialFunctions.LogGamma(Shape) +
            (Shape * Math.Log(Rate)));
    }

    public double Cdf(double value) {
        return value <= 0
            ? 0
            : SpecialFunctions.IncompleteGammaRegularized(Shape, Rate * value);
    }

    public double Survival(double value) {
        return value <= 0
            ? 1
            : SpecialFunctions.IncompleteGammaRegularizedComplement(
                Shape,
                Rate * value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return SpecialFunctions.InverseIncompleteGammaRegularized(
            Shape,
            probability) / Rate;
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return DistributionValidation.SampleStandardGamma(ref generator, Shape) /
            Rate;
    }
}
