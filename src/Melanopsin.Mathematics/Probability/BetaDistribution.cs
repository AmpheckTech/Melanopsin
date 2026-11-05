namespace Melanopsin.Mathematics;

/// <summary>
/// A beta distribution over [0, 1] parameterized by two positive shapes.
/// </summary>
public readonly record struct BetaDistribution : IProbabilityDistribution {
    public BetaDistribution(double firstShape, double secondShape) {
        DistributionValidation.Scale(firstShape, nameof(firstShape));
        DistributionValidation.Scale(secondShape, nameof(secondShape));
        FirstShape = firstShape;
        SecondShape = secondShape;
    }

    public double FirstShape { get; }

    public double SecondShape { get; }

    public double Mean => FirstShape / (FirstShape + SecondShape);

    public double Variance {
        get {
            var sum = FirstShape + SecondShape;
            return (FirstShape * SecondShape) /
                (sum * sum * (sum + 1));
        }
    }

    public double Pdf(double value) {
        if (value < 0 || value > 1 || double.IsNaN(value)) {
            return 0;
        }

        if (value == 0) {
            if (FirstShape < 1) {
                return double.PositiveInfinity;
            }

            return FirstShape == 1 ? SecondShape : 0;
        }

        if (value == 1) {
            if (SecondShape < 1) {
                return double.PositiveInfinity;
            }

            return SecondShape == 1 ? FirstShape : 0;
        }

        return Math.Exp(
            ((FirstShape - 1) * Math.Log(value)) +
            ((SecondShape - 1) * Math.Log(1 - value)) -
            SpecialFunctions.LogBeta(FirstShape, SecondShape));
    }

    public double Cdf(double value) {
        return value <= 0
            ? 0
            : value >= 1
                ? 1
                : SpecialFunctions.IncompleteBetaRegularized(
                    FirstShape,
                    SecondShape,
                    value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        return SpecialFunctions.InverseIncompleteBetaRegularized(
            FirstShape,
            SecondShape,
            probability);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var first = DistributionValidation.SampleStandardGamma(
            ref generator,
            FirstShape);
        var second = DistributionValidation.SampleStandardGamma(
            ref generator,
            SecondShape);
        return first / (first + second);
    }
}
