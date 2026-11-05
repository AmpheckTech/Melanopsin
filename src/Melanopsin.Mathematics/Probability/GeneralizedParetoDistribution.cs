namespace Melanopsin.Mathematics;

/// <summary>
/// A generalized Pareto distribution with location, scale, and shape
/// parameters.
/// </summary>
/// <remarks>
/// For non-negative shape the support is unbounded above. For negative shape
/// the support ends at <c>location - scale / shape</c>, where the continuous
/// density uses its endpoint limit. Moments are reported as infinite when the
/// corresponding shape condition is not met rather than being truncated.
/// </remarks>
public readonly record struct GeneralizedParetoDistribution :
    IProbabilityDistribution {
    public GeneralizedParetoDistribution(
        double location,
        double scale,
        double shape) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        DistributionValidation.Location(shape, nameof(shape));
        Location = location;
        Scale = scale;
        Shape = shape;
    }

    public double Location { get; }

    public double Scale { get; }

    public double Shape { get; }

    /// <summary>
    /// Gets the upper support boundary, or positive infinity for a
    /// non-negative shape.
    /// </summary>
    public double UpperBound => Shape < 0
        ? Location - (Scale / Shape)
        : double.PositiveInfinity;

    public double Mean => Shape < 1
        ? Location + (Scale / (1 - Shape))
        : double.PositiveInfinity;

    public double Variance => Shape < 0.5
        ? Scale * Scale /
            ((1 - Shape) * (1 - Shape) * (1 - (2 * Shape)))
        : double.PositiveInfinity;

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < Location || value > UpperBound) {
            return 0;
        }

        var standardized = (value - Location) / Scale;
        if (Shape == 0) {
            return Math.Exp(-standardized) / Scale;
        }

        var logarithm = FloatingPointMath.Log1p(Shape * standardized);
        return Math.Exp(-(1 + (1 / Shape)) * logarithm) / Scale;
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Location) {
            return 0;
        }

        if (value >= UpperBound) {
            return 1;
        }

        var standardized = (value - Location) / Scale;
        if (Shape == 0) {
            return -FloatingPointMath.Expm1(-standardized);
        }

        var logarithm = FloatingPointMath.Log1p(Shape * standardized);
        return -FloatingPointMath.Expm1(-logarithm / Shape);
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Location) {
            return 1;
        }

        if (value >= UpperBound) {
            return 0;
        }

        var standardized = (value - Location) / Scale;
        return Shape == 0
            ? Math.Exp(-standardized)
            : Math.Exp(-FloatingPointMath.Log1p(Shape * standardized) / Shape);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Location;
        }

        if (probability == 1) {
            return UpperBound;
        }

        var logarithm = -FloatingPointMath.Log1p(-probability);
        return Shape == 0
            ? Location + (Scale * logarithm)
            : Location + (Scale * FloatingPointMath.Expm1(-Shape * logarithm) / Shape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
