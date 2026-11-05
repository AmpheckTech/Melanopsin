namespace Melanopsin.Mathematics;

/// <summary>
/// A Bernoulli distribution over the outcomes zero and one.
/// </summary>
public readonly record struct BernoulliDistribution : IProbabilityDistribution {
    public BernoulliDistribution(double probability) {
        DistributionValidation.Probability(probability);
        Probability = probability;
    }

    /// <summary>Gets the probability of observing one.</summary>
    public double Probability { get; }

    public double Mean => Probability;

    public double Variance => Probability * (1 - Probability);

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value switch {
            0 => 1 - Probability,
            1 => Probability,
            _ => 0,
        };
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at an outcome.
    /// </summary>
    /// <remarks>
    /// Outcomes other than zero and one, and outcomes with probability zero,
    /// return negative infinity. A NaN query returns NaN.
    /// </remarks>
    public double LogPmf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return Probability == 1
                ? double.NegativeInfinity
                : FloatingPointMath.Log1p(-Probability);
        }

        if (value == 1) {
            return Probability == 0
                ? double.NegativeInfinity
                : Math.Log(Probability);
        }

        return double.NegativeInfinity;
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < 0) {
            return 0;
        }

        return value < 1 ? 1 - Probability : 1;
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return probability <= 1 - Probability ? 0 : 1;
    }

    public int Sample(ref Xoshiro256StarStar generator) {
        return generator.NextDouble() < Probability ? 1 : 0;
    }
}
