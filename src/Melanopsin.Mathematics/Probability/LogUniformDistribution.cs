namespace Melanopsin.Mathematics;

/// <summary>
/// A log-uniform distribution over a finite positive interval.
/// </summary>
/// <remarks>
/// The logarithm of an observation is uniformly distributed between the
/// logarithms of the bounds. This distribution is useful for scale parameters
/// spanning several orders of magnitude. Both bounds are strictly positive and
/// the upper bound must be greater than the lower bound.
/// </remarks>
public readonly record struct LogUniformDistribution :
    IProbabilityDistribution {
    public LogUniformDistribution(double minimum, double maximum) {
        DistributionValidation.Scale(minimum, nameof(minimum));
        DistributionValidation.Scale(maximum, nameof(maximum));
        if (maximum <= minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "The maximum must be greater than the minimum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    private double LogRange => Math.Log(Maximum / Minimum);

    public double Mean => (Maximum - Minimum) / LogRange;

    public double Variance {
        get {
            var secondMoment = ((Maximum * Maximum) - (Minimum * Minimum)) /
                (2 * LogRange);
            var mean = Mean;
            return Math.Max(0, secondMoment - (mean * mean));
        }
    }

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value >= Minimum && value <= Maximum
            ? 1 / (value * LogRange)
            : 0;
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        return Math.Log(value / Minimum) / LogRange;
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Minimum) {
            return 1;
        }

        if (value >= Maximum) {
            return 0;
        }

        return Math.Log(Maximum / value) / LogRange;
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Minimum;
        }

        if (probability == 1) {
            return Maximum;
        }

        return Minimum * Math.Exp(probability * LogRange);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
