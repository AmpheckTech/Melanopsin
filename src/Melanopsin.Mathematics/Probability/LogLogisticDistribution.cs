namespace Melanopsin.Mathematics;

/// <summary>
/// A log-logistic distribution over strictly positive values.
/// </summary>
/// <remarks>
/// If <c>log(X / scale) / shape</c> has a standard logistic distribution,
/// then <c>X</c> has this distribution. The shape controls tail weight and the
/// scale is the median. The mean exists only for shape greater than one and
/// the variance only for shape greater than two.
/// </remarks>
public readonly record struct LogLogisticDistribution :
    IProbabilityDistribution {
    public LogLogisticDistribution(double shape, double scale) {
        DistributionValidation.Scale(shape, nameof(shape));
        DistributionValidation.Scale(scale, nameof(scale));
        Shape = shape;
        Scale = scale;
    }

    public double Shape { get; }

    public double Scale { get; }

    public double Mean => Shape > 1
        ? Scale * Constants.Pi / (Shape * Math.Sin(Constants.Pi / Shape))
        : double.PositiveInfinity;

    public double Variance {
        get {
            if (Shape <= 2) {
                return double.PositiveInfinity;
            }

            var sine = Math.Sin(Constants.Pi / Shape);
            return Scale * Scale *
                ((2 * Constants.Pi) / (Shape * Math.Sin(2 * Constants.Pi / Shape)) -
                    (Constants.Pi * Constants.Pi) / (Shape * Shape * sine * sine));
        }
    }

    /// <summary>
    /// Computes the probability density on the positive real line.
    /// </summary>
    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (!(value > 0) || double.IsPositiveInfinity(value)) {
            return 0;
        }

        var standardized = Shape * (Math.Log(value) - Math.Log(Scale));
        var logistic = SpecialFunctions.Sigmoid(standardized);
        return Shape * logistic * (1 - logistic) / value;
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

        var standardized = Shape * (Math.Log(value) - Math.Log(Scale));
        return Math.Log(Shape / value) -
            ElementaryFunctions.Softplus(standardized) -
            ElementaryFunctions.Softplus(-standardized);
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

        return SpecialFunctions.Sigmoid(
            Shape * (Math.Log(value) - Math.Log(Scale)));
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

        return SpecialFunctions.Sigmoid(
            -Shape * (Math.Log(value) - Math.Log(Scale)));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        var logarithm = Math.Log(probability) -
            FloatingPointMath.Log1p(-probability);
        return Scale * Math.Exp(logarithm / Shape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
