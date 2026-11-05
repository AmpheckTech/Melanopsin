namespace Melanopsin.Mathematics;

/// <summary>
/// An Erlang distribution: a gamma distribution whose shape is a positive
/// integer and whose rate is strictly positive.
/// </summary>
/// <remarks>
/// The distribution describes the sum of <see cref="Shape"/> independent
/// exponential random variables with the same rate.
/// </remarks>
public readonly record struct ErlangDistribution : IProbabilityDistribution {
    public ErlangDistribution(int shape, double rate) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shape);
        DistributionValidation.Scale(rate, nameof(rate));
        Shape = shape;
        Rate = rate;
    }

    public int Shape { get; }

    public double Rate { get; }

    public double Scale => 1 / Rate;

    public double Mean => Shape / Rate;

    public double Variance => Shape / (Rate * Rate);

    public double Mode => (Shape - 1.0) / Rate;

    public double Skewness => 2 / Math.Sqrt(Shape);

    public double ExcessKurtosis => 6.0 / Shape;

    /// <summary>
    /// Computes the probability density. Values outside the positive real line
    /// and positive infinity return zero.
    /// </summary>
    public double Pdf(double value) {
        if (!(value > 0) || !double.IsFinite(value)) {
            return value == 0 && Shape == 1 ? Rate : 0;
        }

        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the probability density without first
    /// materializing a small density. Values at or below zero and positive
    /// infinity return negative infinity.
    /// </summary>
    public double LogPdf(double value) {
        if (!(value > 0) || !double.IsFinite(value)) {
            return double.NegativeInfinity;
        }

        return ((Shape - 1.0) * Math.Log(value)) -
            (Rate * value) -
            SpecialFunctions.LogGamma(Shape) +
            (Shape * Math.Log(Rate));
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
