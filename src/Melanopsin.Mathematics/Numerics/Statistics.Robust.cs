namespace Melanopsin.Mathematics;

/// <summary>
/// Robust location and dispersion statistics.
/// </summary>
public static partial class Statistics {
    /// <summary>
    /// Computes the mean absolute deviation from the arithmetic mean.
    /// </summary>
    public static double MeanAbsoluteDeviation(ReadOnlySpan<double> values) {
        var mean = Mean(values);
        var total = 0.0;
        for (var index = 0; index < values.Length; index++) {
            total += Math.Abs(values[index] - mean);
        }

        return total / values.Length;
    }

    /// <summary>
    /// Computes the median absolute deviation from the median.
    /// </summary>
    /// <remarks>
    /// The raw median absolute deviation is returned. The consistency factor
    /// for a normal distribution is deliberately not applied implicitly.
    /// </remarks>
    public static double MedianAbsoluteDeviation(
        ReadOnlySpan<double> values) {
        var median = Median(values);
        var deviations = new double[values.Length];
        for (var index = 0; index < values.Length; index++) {
            deviations[index] = Math.Abs(values[index] - median);
        }

        return Median(deviations);
    }

    /// <summary>Computes the interquartile range.</summary>
    public static double InterquartileRange(ReadOnlySpan<double> values) {
        var copy = values.ToArray();
        Validate(copy);
        copy.AsSpan().Sort();
        return QuantileSorted(copy, 0.75) - QuantileSorted(copy, 0.25);
    }

    /// <summary>
    /// Computes a symmetrically trimmed arithmetic mean.
    /// </summary>
    /// <param name="values">The finite sample.</param>
    /// <param name="proportion">
    /// The fraction removed from each tail. Must be in <c>[0, 0.5)</c>.
    /// </param>
    /// <remarks>
    /// The number removed from each tail is
    /// <c>floor(proportion * count)</c>. At least one central value remains.
    /// </remarks>
    public static double TrimmedMean(
        ReadOnlySpan<double> values,
        double proportion) {
        ValidateTrimProportion(proportion);
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var sorted = values.ToArray();
        Validate(sorted);
        sorted.AsSpan().Sort();
        var trimCount = (int)Math.Floor(proportion * sorted.Length);
        var retainedCount = sorted.Length - (2 * trimCount);
        if (retainedCount <= 0) {
            throw new ArgumentException(
                "The trim proportion removes every observation.",
                nameof(proportion));
        }

        var total = 0.0;
        var correction = 0.0;
        for (var index = trimCount; index < sorted.Length - trimCount; index++) {
            var value = sorted[index];
            var next = total + value;
            if (Math.Abs(total) >= Math.Abs(value)) {
                correction += (total - next) + value;
            } else {
                correction += (value - next) + total;
            }

            total = next;
        }

        return (total + correction) / retainedCount;
    }

    /// <summary>
    /// Computes a symmetrically Winsorized arithmetic mean.
    /// </summary>
    /// <param name="values">The finite sample.</param>
    /// <param name="proportion">
    /// The fraction replaced at each tail. Must be in <c>[0, 0.5)</c>.
    /// </param>
    /// <remarks>
    /// The lowest and highest <c>floor(proportion * count)</c> values are
    /// replaced by the adjacent retained order statistics before averaging.
    /// </remarks>
    public static double WinsorizedMean(
        ReadOnlySpan<double> values,
        double proportion) {
        ValidateTrimProportion(proportion);
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var sorted = values.ToArray();
        Validate(sorted);
        sorted.AsSpan().Sort();
        var replacementCount = (int)Math.Floor(proportion * sorted.Length);
        if (2 * replacementCount >= sorted.Length) {
            throw new ArgumentException(
                "The Winsorization proportion leaves no central observations.",
                nameof(proportion));
        }

        var total = 0.0;
        for (var index = 0; index < sorted.Length; index++) {
            if (index < replacementCount) {
                total += sorted[replacementCount];
            } else if (index >= sorted.Length - replacementCount) {
                total += sorted[sorted.Length - replacementCount - 1];
            } else {
                total += sorted[index];
            }
        }

        return total / sorted.Length;
    }

    /// <summary>Computes the standard error of the arithmetic mean.</summary>
    public static double StandardErrorOfMean(ReadOnlySpan<double> values) {
        return StandardDeviationSample(values) / Math.Sqrt(values.Length);
    }

    /// <summary>
    /// Computes the sample coefficient of variation.
    /// </summary>
    public static double CoefficientOfVariation(ReadOnlySpan<double> values) {
        return StandardDeviationSample(values) / Mean(values);
    }

    private static double QuantileSorted(
        ReadOnlySpan<double> sorted,
        double probability) {
        var position = (sorted.Length - 1) * probability;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        var fraction = position - lowerIndex;
        return sorted[lowerIndex] +
            (fraction * (sorted[upperIndex] - sorted[lowerIndex]));
    }

    private static void ValidateTrimProportion(double proportion) {
        if (!double.IsFinite(proportion) || proportion < 0 || proportion >= 0.5) {
            throw new ArgumentOutOfRangeException(
                nameof(proportion),
                "The proportion must be finite and in [0, 0.5).");
        }
    }
}
