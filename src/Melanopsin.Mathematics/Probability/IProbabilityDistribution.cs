namespace Melanopsin.Mathematics;

/// <summary>
/// A one-dimensional probability distribution with a probability density or
/// mass function over real-valued observations.
/// </summary>
public interface IProbabilityDistribution {
    double Mean { get; }

    double Variance { get; }

    double Pdf(double value);

    double Cdf(double value);

    double Quantile(double probability);
}
