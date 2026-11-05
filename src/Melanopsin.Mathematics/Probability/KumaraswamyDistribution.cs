namespace Melanopsin.Mathematics;

/// <summary>
/// A Kumaraswamy distribution on the open unit interval.
/// </summary>
/// <remarks>
/// The density may be unbounded at an endpoint when a shape is below one.
/// Endpoint queries use the continuous extension of the CDF, while the
/// quantile maps probabilities zero and one to the respective endpoints.
/// The distribution was introduced by
/// <see href="https://doi.org/10.1016/0022-1694(80)90036-0">Kumaraswamy (1980)</see>.
/// </remarks>
public readonly record struct KumaraswamyDistribution : IProbabilityDistribution {
    public KumaraswamyDistribution(double firstShape, double secondShape) {
        DistributionValidation.Scale(firstShape, nameof(firstShape));
        DistributionValidation.Scale(secondShape, nameof(secondShape));
        FirstShape = firstShape;
        SecondShape = secondShape;
    }

    public double FirstShape { get; }

    public double SecondShape { get; }

    public double Mean =>
        SecondShape * Math.Exp(
            SpecialFunctions.LogBeta(1 + (1 / FirstShape), SecondShape));

    public double Variance {
        get {
            var secondMoment = SecondShape * Math.Exp(
                SpecialFunctions.LogBeta(
                    1 + (2 / FirstShape),
                    SecondShape));
            var mean = Mean;
            return Math.Max(0, secondMoment - (mean * mean));
        }
    }

    public double Pdf(double value) {
        if (double.IsNaN(value) || value < 0 || value > 1) {
            return double.IsNaN(value) ? double.NaN : 0;
        }

        if (value == 0) {
            return FirstShape < 1 ? double.PositiveInfinity : FirstShape == 1
                ? FirstShape * SecondShape
                : 0;
        }

        if (value == 1) {
            return SecondShape < 1 ? double.PositiveInfinity : SecondShape == 1
                ? FirstShape * SecondShape
                : 0;
        }

        var logarithm =
            Math.Log(FirstShape) +
            Math.Log(SecondShape) +
            ((FirstShape - 1) * Math.Log(value)) +
            ((SecondShape - 1) * FloatingPointMath.Log1p(-Math.Pow(value, FirstShape)));
        return Math.Exp(logarithm);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= 0) {
            return 0;
        }

        if (value >= 1) {
            return 1;
        }

        return -FloatingPointMath.Expm1(
            SecondShape * FloatingPointMath.Log1p(-Math.Pow(value, FirstShape)));
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= 0) {
            return 1;
        }

        if (value >= 1) {
            return 0;
        }

        return Math.Pow(1 - Math.Pow(value, FirstShape), SecondShape);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return 1;
        }

        var logarithm = FloatingPointMath.Log1p(-probability) / SecondShape;
        return Math.Pow(
            -FloatingPointMath.Expm1(logarithm),
            1 / FirstShape);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
