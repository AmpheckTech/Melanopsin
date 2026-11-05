namespace Melanopsin.Mathematics;

/// <summary>
/// A finite categorical distribution over the integer category indices
/// <c>0</c> through <c>Count - 1</c>.
/// </summary>
/// <remarks>
/// Constructor weights may be any finite non-negative values with a positive
/// total. They are normalized and copied, so later changes to the caller's
/// buffer do not affect the distribution. Category order is significant and is
/// retained exactly; this type does not infer labels.
/// </remarks>
public sealed class CategoricalDistribution : IProbabilityDistribution {
    private readonly double[] _probabilities;

    public CategoricalDistribution(ReadOnlySpan<double> weights) {
        if (weights.IsEmpty) {
            throw new ArgumentException(
                "At least one category weight is required.",
                nameof(weights));
        }

        var total = 0.0;
        for (var index = 0; index < weights.Length; index++) {
            var weight = weights[index];
            if (!double.IsFinite(weight) || weight < 0) {
                throw new ArgumentException(
                    "Category weights must be finite and non-negative.",
                    nameof(weights));
            }

            total += weight;
        }

        if (!(total > 0) || !double.IsFinite(total)) {
            throw new ArgumentException(
                "Category weights must have a finite positive total.",
                nameof(weights));
        }

        _probabilities = new double[weights.Length];
        for (var index = 0; index < weights.Length; index++) {
            _probabilities[index] = weights[index] / total;
        }
    }

    public int Count => _probabilities.Length;

    public ReadOnlySpan<double> Probabilities => _probabilities;

    public double Mean {
        get {
            var result = 0.0;
            for (var index = 0; index < _probabilities.Length; index++) {
                result += index * _probabilities[index];
            }

            return result;
        }
    }

    public double Variance {
        get {
            var mean = Mean;
            var result = 0.0;
            for (var index = 0; index < _probabilities.Length; index++) {
                var difference = index - mean;
                result += difference * difference * _probabilities[index];
            }

            return result;
        }
    }

    /// <summary>Gets the highest-probability category, choosing the lowest index on ties.</summary>
    public int Mode {
        get {
            var result = 0;
            for (var index = 1; index < _probabilities.Length; index++) {
                if (_probabilities[index] > _probabilities[result]) {
                    result = index;
                }
            }

            return result;
        }
    }

    public double Entropy {
        get {
            var result = 0.0;
            for (var index = 0; index < _probabilities.Length; index++) {
                var probability = _probabilities[index];
                if (probability > 0) {
                    result -= probability * Math.Log(probability);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at a category.
    /// </summary>
    /// <remarks>
    /// Values outside the category range or non-integer values have zero
    /// probability and return <see cref="double.NegativeInfinity"/>. A
    /// zero-probability category also returns negative infinity.
    /// </remarks>
    public double LogPmf(int category) {
        if (category < 0 || category >= _probabilities.Length) {
            return double.NegativeInfinity;
        }

        var probability = _probabilities[category];
        return probability == 0 ? double.NegativeInfinity : Math.Log(probability);
    }

    /// <summary>
    /// Computes the cumulative probability through a category index.
    /// </summary>
    /// <remarks>
    /// Negative indices return zero and indices at or beyond the final category
    /// return one. This method exposes the discrete convention directly and
    /// avoids floating-point truncation of a query value.
    /// </remarks>
    public double CdfThrough(int category) {
        if (category < 0) {
            return 0;
        }

        if (category >= _probabilities.Length - 1) {
            return 1;
        }

        var result = 0.0;
        for (var index = 0; index <= category; index++) {
            result += _probabilities[index];
        }

        return result;
    }

    public double Pdf(double value) {
        if (!double.IsFinite(value) ||
            value != Math.Truncate(value) ||
            value < 0 ||
            value >= _probabilities.Length) {
            return 0;
        }

        return _probabilities[(int)value];
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < 0) {
            return 0;
        }

        if (value >= _probabilities.Length - 1) {
            return 1;
        }

        var lastCategory = (int)Math.Floor(value);
        var result = 0.0;
        for (var index = 0; index <= lastCategory; index++) {
            result += _probabilities[index];
        }

        return result;
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 1) {
            return _probabilities.Length - 1;
        }

        var cumulative = 0.0;
        for (var index = 0; index < _probabilities.Length; index++) {
            cumulative += _probabilities[index];
            if (probability <= cumulative) {
                return index;
            }
        }

        return _probabilities.Length - 1;
    }

    public int Sample(ref Xoshiro256StarStar generator) {
        var value = generator.NextDouble();
        var cumulative = 0.0;
        for (var index = 0; index < _probabilities.Length; index++) {
            cumulative += _probabilities[index];
            if (value < cumulative) {
                return index;
            }
        }

        return _probabilities.Length - 1;
    }
}
