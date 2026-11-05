namespace Melanopsin.Mathematics;

/// <summary>
/// A Zipf distribution over the integers from one through
/// <see cref="Count"/>, with probability mass proportional to
/// <c>k^-Exponent</c>.
/// </summary>
/// <remarks>
/// Negative exponents are permitted mathematically when the finite normalizing
/// sum remains representable. The normalization constant is computed once at
/// construction. Density and cumulative queries are deterministic and
/// allocation-free, but take time linear in <see cref="Count"/>.
/// </remarks>
public readonly record struct ZipfDistribution : IProbabilityDistribution {
    private readonly double _normalization;

    public ZipfDistribution(int count, double exponent) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (!double.IsFinite(exponent)) {
            throw new ArgumentOutOfRangeException(nameof(exponent));
        }

        Count = count;
        Exponent = exponent;
        var normalization = ComputeNormalization(count, exponent);
        if (!double.IsFinite(normalization) || normalization <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(exponent),
                "The finite Zipf normalization must be positive and finite.");
        }

        _normalization = normalization;
    }

    public int Count { get; }

    public double Exponent { get; }

    public double Normalization => _normalization;

    public double Mean {
        get {
            var sum = 0.0;
            for (var value = 1; value <= Count; value++) {
                sum += Math.Pow(value, 1 - Exponent);
            }

            return sum / _normalization;
        }
    }

    public double Variance {
        get {
            var secondMoment = 0.0;
            for (var value = 1; value <= Count; value++) {
                secondMoment += Math.Pow(value, 2 - Exponent);
            }

            var mean = Mean;
            return Math.Max(0, (secondMoment / _normalization) - (mean * mean));
        }
    }

    public double Pdf(double value) {
        if (value < 1 || value > Count || value != Math.Truncate(value)) {
            return 0;
        }

        return Math.Pow(value, -Exponent) / _normalization;
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at an integer.
    /// </summary>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 1 ||
            value > Count ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        return (-Exponent * Math.Log(value)) - Math.Log(_normalization);
    }

    public double Cdf(double value) {
        if (value < 1) {
            return 0;
        }

        if (value >= Count) {
            return 1;
        }

        var maximum = (int)Math.Floor(value);
        var sum = 0.0;
        for (var current = 1; current <= maximum; current++) {
            sum += Math.Pow(current, -Exponent);
        }

        return Math.Clamp(sum / _normalization, 0, 1);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 1;
        }

        if (probability == 1) {
            return Count;
        }

        var target = probability * _normalization;
        var cumulative = 0.0;
        for (var value = 1; value <= Count; value++) {
            cumulative += Math.Pow(value, -Exponent);
            if (cumulative >= target) {
                return value;
            }
        }

        return Count;
    }

    public int Sample(ref Xoshiro256StarStar generator) {
        return (int)Quantile(generator.NextDouble());
    }

    private static double ComputeNormalization(int count, double exponent) {
        var sum = 0.0;
        for (var value = 1; value <= count; value++) {
            sum += Math.Pow(value, -exponent);
        }

        return sum;
    }
}
