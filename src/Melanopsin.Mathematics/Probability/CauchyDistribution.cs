namespace Melanopsin.Mathematics;

/// <summary>
/// A Cauchy distribution parameterized by location and scale.
/// </summary>
/// <remarks>
/// The mean and variance do not exist and are represented by NaN.
/// </remarks>
public readonly record struct CauchyDistribution : IProbabilityDistribution {
    public CauchyDistribution(double location, double scale) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        Location = location;
        Scale = scale;
    }

    public double Location { get; }

    public double Scale { get; }

    public double Mean => double.NaN;

    public double Variance => double.NaN;

    public double Pdf(double value) {
        var standardized = (value - Location) / Scale;
        return 1 / (Constants.Pi * Scale * (1 + (standardized * standardized)));
    }

    public double Cdf(double value) {
        return 0.5 + (Math.Atan((value - Location) / Scale) / Constants.Pi);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Location + (Scale * Math.Tan(Constants.Pi * (probability - 0.5)));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
