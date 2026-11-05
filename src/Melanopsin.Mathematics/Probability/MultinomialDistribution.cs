namespace Melanopsin.Mathematics;

/// <summary>
/// A multinomial distribution for a fixed number of independent trials and a
/// fixed number of outcome categories.
/// </summary>
/// <remarks>
/// Supplied category probabilities are finite, non-negative, and must have a
/// positive sum. They are normalized on construction to absorb reasonable
/// caller rounding. Probability mass is reported at non-negative integer count
/// vectors whose entries sum exactly to <see cref="TrialCount"/>.
/// </remarks>
public sealed class MultinomialDistribution {
    private readonly double[] _probabilities;

    public MultinomialDistribution(
        int trialCount,
        ReadOnlySpan<double> probabilities) {
        ArgumentOutOfRangeException.ThrowIfNegative(trialCount);
        if (probabilities.Length == 0) {
            throw new ArgumentException(
                "At least one outcome probability is required.",
                nameof(probabilities));
        }

        var scale = 0.0;
        for (var index = 0; index < probabilities.Length; index++) {
            var value = probabilities[index];
            if (!double.IsFinite(value) || value < 0) {
                throw new ArgumentException(
                    "Outcome probabilities must be finite and non-negative.",
                    nameof(probabilities));
            }

            scale = Math.Max(scale, value);
        }

        if (scale == 0) {
            throw new ArgumentException(
                "At least one outcome probability must be positive.",
                nameof(probabilities));
        }

        _probabilities = new double[probabilities.Length];
        var sum = 0.0;
        for (var index = 0; index < probabilities.Length; index++) {
            var scaled = probabilities[index] / scale;
            _probabilities[index] = scaled;
            sum += scaled;
        }

        var inverseSum = 1 / sum;
        for (var index = 0; index < _probabilities.Length; index++) {
            _probabilities[index] *= inverseSum;
        }

        TrialCount = trialCount;
    }

    public int TrialCount { get; }

    public int CategoryCount => _probabilities.Length;

    public ReadOnlySpan<double> Probabilities => _probabilities;

    public double Mean(int category) {
        return TrialCount * GetProbability(category);
    }

    public double Variance(int category) {
        var probability = GetProbability(category);
        return TrialCount * probability * (1 - probability);
    }

    public double Covariance(int firstCategory, int secondCategory) {
        var first = GetProbability(firstCategory);
        var second = GetProbability(secondCategory);
        return firstCategory == secondCategory
            ? Variance(firstCategory)
            : -TrialCount * first * second;
    }

    /// <summary>
    /// Computes the probability mass at an integer count vector.
    /// </summary>
    public double Pmf(ReadOnlySpan<int> counts) {
        return Math.Exp(LogPmf(counts));
    }

    /// <summary>
    /// Computes the log probability mass at an integer count vector.
    /// Invalid count vectors return negative infinity.
    /// </summary>
    public double LogPmf(ReadOnlySpan<int> counts) {
        if (counts.Length < CategoryCount) {
            throw new ArgumentException(
                "The count vector is too small.",
                nameof(counts));
        }

        long total = 0;
        var result = SpecialFunctions.LogGamma(TrialCount + 1.0);
        for (var index = 0; index < CategoryCount; index++) {
            var count = counts[index];
            if (count < 0) {
                return double.NegativeInfinity;
            }

            total += count;
            var probability = _probabilities[index];
            if (count > 0 && probability == 0) {
                return double.NegativeInfinity;
            }

            result -= SpecialFunctions.LogGamma(count + 1.0);
            if (count > 0) {
                result += count * Math.Log(probability);
            }
        }

        return total == TrialCount
            ? result
            : double.NegativeInfinity;
    }

    /// <summary>
    /// Draws a count vector. The destination is overwritten and allocates
    /// nothing.
    /// </summary>
    public void Sample(
        Span<int> destination,
        ref Xoshiro256StarStar generator) {
        _ = TrySample(destination, ref generator, out _);
    }

    /// <summary>
    /// Attempts to draw a count vector without allocating.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the destination is large enough and receives
    /// a valid sample.
    /// </returns>
    public bool TrySample(
        Span<int> destination,
        ref Xoshiro256StarStar generator) {
        return TrySample(destination, ref generator, out _);
    }

    /// <summary>
    /// Attempts to draw a count vector and returns a diagnostic report.
    /// </summary>
    /// <remarks>
    /// Sampling itself has no numerical singularity for a valid multinomial
    /// distribution. The report is provided for consistency with the other
    /// distribution sampling APIs.
    /// </remarks>
    public bool TrySample(
        Span<int> destination,
        ref Xoshiro256StarStar generator,
        out DistributionOperationReport report) {
        if (destination.Length < CategoryCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        var remaining = TrialCount;
        var remainingProbability = 1.0;
        for (var index = 0; index < CategoryCount - 1; index++) {
            var probability = _probabilities[index];
            if (remaining == 0 || probability == 0) {
                destination[index] = 0;
            } else {
                var conditionalProbability = Math.Clamp(
                    probability / remainingProbability,
                    0,
                    1);
                var draw = new BinomialDistribution(
                    remaining,
                    conditionalProbability).Sample(ref generator);
                var count = (int)draw;
                destination[index] = count;
                remaining -= count;
            }

            remainingProbability -= probability;
        }

        destination[CategoryCount - 1] = remaining;
        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            DistributionStatus.Succeeded);
        return true;
    }

    private double GetProbability(int category) {
        if ((uint)category >= (uint)CategoryCount) {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        return _probabilities[category];
    }
}
