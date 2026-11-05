namespace Melanopsin.Mathematics;

/// <summary>
/// An exponential distribution parameterized by rate.
/// </summary>
public readonly record struct ExponentialDistribution : IProbabilityDistribution {
    public ExponentialDistribution(double rate) {
        DistributionValidation.Scale(rate, nameof(rate));
        Rate = rate;
    }

    public double Rate { get; }

    public double Mean => 1 / Rate;

    public double Variance => 1 / (Rate * Rate);

    public double Pdf(double value) {
        return value < 0 ? 0 : Rate * Math.Exp(-Rate * value);
    }

    public double Cdf(double value) {
        return value <= 0 ? 0 : -FloatingPointMath.Expm1(-Rate * value);
    }

    public double Survival(double value) {
        return value <= 0 ? 1 : Math.Exp(-Rate * value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return probability == 1
            ? double.PositiveInfinity
            : -FloatingPointMath.Log1p(-probability) / Rate;
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return -Math.Log(1 - generator.NextDouble()) / Rate;
    }
}
