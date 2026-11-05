namespace Melanopsin.Mathematics;

/// <summary>A half-open histogram bin, with the final bin closed on the right.</summary>
public readonly record struct HistogramBin(
    double LowerBound,
    double UpperBound,
    int Count);

/// <summary>An immutable equal-width histogram.</summary>
public sealed class Histogram {
    private readonly HistogramBin[] _bins;

    internal Histogram(HistogramBin[] bins) {
        _bins = bins;
        for (var index = 0; index < bins.Length; index++) {
            Count += bins[index].Count;
        }
    }

    public ReadOnlySpan<HistogramBin> Bins => _bins;

    public int Count { get; }

    public int BinCount => _bins.Length;
}

public static partial class Statistics {
    /// <summary>
    /// Computes an equal-width histogram over the finite range of a sample.
    /// </summary>
    /// <remarks>
    /// The interval is partitioned into half-open bins, except that the final
    /// bin includes its upper endpoint. If every value is identical, the
    /// result contains one zero-width bin as a deliberate degenerate case.
    /// </remarks>
    public static Histogram Histogram(
        ReadOnlySpan<double> values,
        int binCount) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var minimum = values[0];
        var maximum = values[0];
        for (var index = 1; index < values.Length; index++) {
            minimum = Math.Min(minimum, values[index]);
            maximum = Math.Max(maximum, values[index]);
        }

        if (minimum == maximum) {
            return new Histogram([new HistogramBin(minimum, maximum, values.Length)]);
        }

        var width = (maximum - minimum) / binCount;
        var bins = new HistogramBin[binCount];
        var counts = new int[binCount];
        for (var index = 0; index < values.Length; index++) {
            var bin = values[index] == maximum
                ? binCount - 1
                : Math.Min(
                    binCount - 1,
                    (int)((values[index] - minimum) / width));
            counts[bin]++;
        }

        for (var index = 0; index < binCount; index++) {
            var lower = minimum + (index * width);
            var upper = index == binCount - 1
                ? maximum
                : minimum + ((index + 1) * width);
            bins[index] = new HistogramBin(lower, upper, counts[index]);
        }

        return new Histogram(bins);
    }
}
