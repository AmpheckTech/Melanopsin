namespace Melanopsin.Mathematics;

/// <summary>
/// A Pareto distribution with support [scale, positive infinity).
/// </summary>
/// <remarks>
/// The mean is finite only for shape greater than one, and the variance is
/// finite only for shape greater than two. Infinite parameters therefore
/// return infinite moments instead of silently substituting a truncated tail.
/// </remarks>
public readonly record struct ParetoDistribution : IProbabilityDistribution {
    public ParetoDistribution(double scale, double shape) {
        DistributionValidation.Scale(scale, nameof(scale));
        DistributionValidation.Scale(shape, nameof(shape));
        Scale = scale;
        Shape = shape;
    }

    public double Scale { get; }

    public double Shape { get; }

    public double Mean => Shape > 1
        ? Shape * Scale / (Shape - 1)
        : double.PositiveInfinity;

    public double Variance => Shape > 2
        ? Scale * Scale * Shape /
            ((Shape - 1) * (Shape - 1) * (Shape - 2))
        : double.PositiveInfinity;

    public double Pdf(double value) {
        if (value < Scale || !double.IsFinite(value)) {
            return 0;
        }

        return Math.Exp(
            Math.Log(Shape) +
            (Shape * Math.Log(Scale)) -
            ((Shape + 1) * Math.Log(value)));
    }

    public double Cdf(double value) {
        if (value <= Scale) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        return -FloatingPointMath.Expm1(
            Shape * Math.Log(Scale / value));
    }

    public double Survival(double value) {
        if (value <= Scale) {
            return 1;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        return Math.Pow(Scale / value, Shape);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Scale;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Scale / Math.Pow(1 - probability, 1 / Shape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
