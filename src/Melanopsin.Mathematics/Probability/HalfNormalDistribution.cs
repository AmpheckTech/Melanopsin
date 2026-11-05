namespace Melanopsin.Mathematics;

/// <summary>
/// A half-normal distribution on non-negative real values, parameterized by
/// the scale of the underlying zero-mean normal distribution.
/// </summary>
public readonly record struct HalfNormalDistribution : IProbabilityDistribution {
    public HalfNormalDistribution(double scale) {
        DistributionValidation.Scale(scale, nameof(scale));
        Scale = scale;
    }

    public double Scale { get; }

    public double Mean => Scale * Constants.Sqrt2 * Constants.InverseSqrtPi;

    public double Variance => Scale * Scale * (1 - (2 * Constants.InvPi));

    public double Pdf(double value) {
        if (double.IsNaN(value) || value < 0) {
            return value == double.NegativeInfinity ? 0 : double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        var standardized = value / Scale;
        return Constants.Sqrt2 * Constants.InverseSqrtPi / Scale *
            Math.Exp(-0.5 * standardized * standardized);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value <= 0
            ? 0
            : double.IsPositiveInfinity(value)
                ? 1
                : SpecialFunctions.Erf(
                    value / (Scale * Constants.Sqrt2));
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value <= 0
            ? 1
            : double.IsPositiveInfinity(value)
                ? 0
                : SpecialFunctions.Erfc(
                    value / (Scale * Constants.Sqrt2));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return probability == 1
            ? double.PositiveInfinity
            : Scale * Constants.Sqrt2 *
                SpecialFunctions.InverseErf(probability);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Math.Abs(
            DistributionValidation.SampleNormal(ref generator) * Scale);
    }
}
