namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable empirical distribution of finite real observations.
/// </summary>
/// <remarks>
/// The CDF is right-continuous and is defined as the fraction of observations
/// less than or equal to a query value. The quantile is the generalized inverse
/// with the lower order-statistic convention:
/// <c>ceil(probability * count) - 1</c> for probabilities strictly between zero
/// and one. Repeated observations are retained with their multiplicity.
/// </remarks>
public sealed class EmpiricalDistribution {
    private readonly double[] _sortedValues;

    public EmpiricalDistribution(ReadOnlySpan<double> observations) {
        if (observations.IsEmpty) {
            throw new ArgumentException(
                "At least one observation is required.",
                nameof(observations));
        }

        _sortedValues = observations.ToArray();
        Validate(_sortedValues);
        Array.Sort(_sortedValues);
        Mean = Statistics.Mean(_sortedValues);
        PopulationVariance = Statistics.VariancePopulation(_sortedValues);
    }

    public int Count => _sortedValues.Length;

    public double Minimum => _sortedValues[0];

    public double Maximum => _sortedValues[^1];

    public double Mean { get; }

    public double PopulationVariance { get; }

    public double SampleVariance => Count < 2
        ? double.NaN
        : Statistics.VarianceSample(_sortedValues);

    public double Median => Quantile(0.5);

    public ReadOnlySpan<double> SortedValues => _sortedValues;

    /// <summary>Evaluates the right-continuous empirical CDF.</summary>
    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        var lower = 0;
        var upper = _sortedValues.Length;
        while (lower < upper) {
            var middle = lower + ((upper - lower) / 2);
            if (_sortedValues[middle] <= value) {
                lower = middle + 1;
            } else {
                upper = middle;
            }
        }

        return (double)lower / _sortedValues.Length;
    }

    public double Survival(double value) {
        return 1 - Cdf(value);
    }

    public double Quantile(double probability) {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        if (probability == 0) {
            return Minimum;
        }

        var index = (int)Math.Ceiling(probability * Count) - 1;
        return _sortedValues[Math.Clamp(index, 0, Count - 1)];
    }

    private static void Validate(ReadOnlySpan<double> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "Observations must be finite.",
                    nameof(values));
            }
        }
    }
}
