// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A compensated mean and variance summary.
/// </summary>
public readonly record struct MeanVarianceResult {
    public MeanVarianceResult(
        double mean,
        double populationVariance,
        int count) {
        Mean = mean;
        PopulationVariance = populationVariance;
        Count = count;
    }

    public double Mean { get; }

    public double PopulationVariance { get; }

    public double SampleVariance => Count < 2
        ? double.NaN
        : PopulationVariance * Count / (Count - 1);

    public int Count { get; }

    public double Variance => PopulationVariance;
}

/// <summary>
/// Allocation-free descriptive statistics for finite real samples.
/// </summary>
/// <remarks>
/// The default quantile definition uses linear interpolation between order
/// statistics at position <c>(count - 1) * probability</c>. Alternative
/// definitions are explicit. Variance and standard deviation have explicit
/// population and sample variants; no implicit denominator choice is made.
/// </remarks>
public static partial class Statistics {
    /// <summary>
    /// Computes a compensated sum.
    /// </summary>
    /// <remarks>
    /// The accumulation uses Neumaier's compensation rule to reduce the error
    /// of naive floating-point summation:
    /// <see href="https://doi.org/10.1002/zamm.19740540106">Neumaier (1974)</see>.
    /// </remarks>
    public static double Sum(ReadOnlySpan<double> values) {
        Validate(values);
        var sum = 0.0;
        var correction = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            var next = sum + value;
            if (Math.Abs(sum) >= Math.Abs(value)) {
                correction += (sum - next) + value;
            } else {
                correction += (value - next) + sum;
            }

            sum = next;
        }

        return sum + correction;
    }

    public static double Mean(ReadOnlySpan<double> values) {
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        return Sum(values) / values.Length;
    }

    public static double Minimum(ReadOnlySpan<double> values) {
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var result = values[0];
        for (var index = 1; index < values.Length; index++) {
            if (values[index] < result) {
                result = values[index];
            }
        }

        return result;
    }

    public static double Maximum(ReadOnlySpan<double> values) {
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var result = values[0];
        for (var index = 1; index < values.Length; index++) {
            if (values[index] > result) {
                result = values[index];
            }
        }

        return result;
    }

    /// <summary>
    /// Computes a numerically stable population variance with Welford's
    /// recurrence.
    /// </summary>
    /// <remarks>
    /// The online update follows
    /// <see href="https://doi.org/10.1080/00401706.1962.10490022">Welford (1962)</see>.
    /// </remarks>
    public static double VariancePopulation(ReadOnlySpan<double> values) {
        return ComputeMeanVariance(values).PopulationVariance;
    }

    /// <summary>
    /// Computes the unbiased sample variance.
    /// </summary>
    public static double VarianceSample(ReadOnlySpan<double> values) {
        if (values.Length < 2) {
            throw new ArgumentException(
                "At least two values are required for sample variance.",
                nameof(values));
        }

        return ComputeMeanVariance(values).SampleVariance;
    }

    public static double StandardDeviationPopulation(
        ReadOnlySpan<double> values) {
        return Math.Sqrt(VariancePopulation(values));
    }

    public static double StandardDeviationSample(
        ReadOnlySpan<double> values) {
        return Math.Sqrt(VarianceSample(values));
    }

    public static double Median(ReadOnlySpan<double> values) {
        return Quantile(values, 0.5);
    }

    public static double Quantile(
        ReadOnlySpan<double> values,
        double probability) {
        return Quantile(values, probability, QuantileDefinition.Linear);
    }

    /// <summary>
    /// Copies and sorts the sample, then evaluates the requested sample
    /// quantile definition.
    /// </summary>
    public static double Quantile(
        ReadOnlySpan<double> values,
        double probability,
        QuantileDefinition definition) {
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var copy = values.ToArray();
        return QuantileInPlace(copy, probability, definition);
    }

    /// <summary>
    /// Sorts <paramref name="values"/> in place and evaluates the linear
    /// quantile at plotting position <c>(count - 1) * probability</c>.
    /// </summary>
    public static double QuantileInPlace(
        Span<double> values,
        double probability) {
        return QuantileInPlace(values, probability, QuantileDefinition.Linear);
    }

    /// <summary>
    /// Sorts <paramref name="values"/> in place and evaluates the requested
    /// sample-quantile definition.
    /// </summary>
    /// <remarks>
    /// The operation is deterministic and allocation-free. The default
    /// two-argument overload uses <see cref="QuantileDefinition.Linear"/>,
    /// whose plotting positions are <c>(k - 1) / (count - 1)</c> and therefore
    /// range from zero to one.
    /// </remarks>
    public static double QuantileInPlace(
        Span<double> values,
        double probability,
        QuantileDefinition definition) {
        ValidateProbability(probability);
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        if (!Enum.IsDefined(definition)) {
            throw new ArgumentOutOfRangeException(nameof(definition));
        }

        Validate(values);
        values.Sort();
        var count = values.Length;
        if (count == 1) {
            return values[0];
        }

        if (probability == 0) {
            return values[0];
        }

        if (probability == 1) {
            return values[^1];
        }

        var position = count * probability;
        return definition switch {
            QuantileDefinition.InverseEmpirical =>
                values[Math.Clamp((int)Math.Ceiling(position), 1, count) - 1],
            QuantileDefinition.AveragedInverseEmpirical =>
                AveragedInverseEmpirical(values, position),
            QuantileDefinition.NearestEven =>
                values[Math.Clamp(
                    (int)Math.Round(position, MidpointRounding.ToEven),
                    1,
                    count) - 1],
            QuantileDefinition.LinearWithEndpoints =>
                InterpolateOneBased(values, position),
            QuantileDefinition.Midpoint =>
                InterpolateOneBased(values, position + 0.5),
            QuantileDefinition.Weibull =>
                InterpolateOneBased(values, (count + 1.0) * probability),
            QuantileDefinition.Linear =>
                InterpolateOneBased(values, 1 + ((count - 1.0) * probability)),
            QuantileDefinition.MedianUnbiased =>
                InterpolateOneBased(
                    values,
                    ((count + (1.0 / 3)) * probability) + (1.0 / 3)),
            QuantileDefinition.NormalUnbiased =>
                InterpolateOneBased(
                    values,
                    ((count + 0.25) * probability) + 0.375),
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };
    }

    public static double CovariancePopulation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        return ComputeCovariance(x, y) / x.Length;
    }

    public static double CovarianceSample(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        ValidateCovarianceInputs(x, y);
        if (x.Length < 2) {
            throw new ArgumentException(
                "At least two paired values are required.",
                nameof(x));
        }

        return ComputeCovariance(x, y) / (x.Length - 1);
    }

    /// <summary>
    /// Computes the Pearson correlation coefficient.
    /// </summary>
    public static double Correlation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        ValidateCovarianceInputs(x, y);
        if (x.Length < 2) {
            throw new ArgumentException(
                "At least two paired values are required.",
                nameof(x));
        }

        var meanX = 0.0;
        var meanY = 0.0;
        var sumSquaresX = 0.0;
        var sumSquaresY = 0.0;
        var sumProducts = 0.0;
        for (var index = 0; index < x.Length; index++) {
            var count = index + 1;
            var deltaX = x[index] - meanX;
            var deltaY = y[index] - meanY;
            meanX += deltaX / count;
            meanY += deltaY / count;
            sumSquaresX += deltaX * (x[index] - meanX);
            sumSquaresY += deltaY * (y[index] - meanY);
            sumProducts += deltaX * (y[index] - meanY);
        }

        var denominator = Math.Sqrt(sumSquaresX * sumSquaresY);
        if (denominator == 0) {
            return double.NaN;
        }

        return Math.Clamp(sumProducts / denominator, -1, 1);
    }

    /// <summary>
    /// Computes the population skewness using the third central moment.
    /// </summary>
    public static double SkewnessPopulation(ReadOnlySpan<double> values) {
        var summary = ComputeMeanVariance(values);
        if (values.Length == 0 || summary.PopulationVariance == 0) {
            return double.NaN;
        }

        var sumCubes = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var difference = values[index] - summary.Mean;
            sumCubes += difference * difference * difference;
        }

        var deviation = Math.Sqrt(summary.PopulationVariance);
        return sumCubes / values.Length / (deviation * deviation * deviation);
    }

    /// <summary>
    /// Computes the excess population kurtosis.
    /// </summary>
    public static double ExcessKurtosisPopulation(
        ReadOnlySpan<double> values) {
        var summary = ComputeMeanVariance(values);
        if (values.Length == 0 || summary.PopulationVariance == 0) {
            return double.NaN;
        }

        var sumFourthPowers = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var difference = values[index] - summary.Mean;
            var square = difference * difference;
            sumFourthPowers += square * square;
        }

        var varianceSquared = summary.PopulationVariance * summary.PopulationVariance;
        return (sumFourthPowers / values.Length / varianceSquared) - 3;
    }

    public static double WeightedMean(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights) {
        ValidateWeightedInputs(values, weights);
        var weightedSum = 0.0;
        var totalWeight = 0.0;
        for (var index = 0; index < values.Length; index++) {
            weightedSum += values[index] * weights[index];
            totalWeight += weights[index];
        }

        if (totalWeight <= 0) {
            throw new ArgumentException(
                "The sum of weights must be positive.",
                nameof(weights));
        }

        return weightedSum / totalWeight;
    }

    /// <remarks>
    /// This is Welford's recurrence; its covariance analogue below uses the
    /// same online-update idea.
    /// </remarks>
    private static MeanVarianceResult ComputeMeanVariance(
        ReadOnlySpan<double> values) {
        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var mean = 0.0;
        var sumSquares = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var count = index + 1;
            var difference = values[index] - mean;
            mean += difference / count;
            sumSquares += difference * (values[index] - mean);
        }

        return new MeanVarianceResult(
            mean,
            sumSquares / values.Length,
            values.Length);
    }

    /// <remarks>
    /// The online co-moment update extends the recurrence described by
    /// <see href="https://doi.org/10.1080/00401706.1962.10490022">Welford (1962)</see>
    /// to paired samples.
    /// </remarks>
    private static double ComputeCovariance(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        ValidateCovarianceInputs(x, y);
        var meanX = 0.0;
        var meanY = 0.0;
        var coMoment = 0.0;
        for (var index = 0; index < x.Length; index++) {
            var count = index + 1;
            var deltaX = x[index] - meanX;
            meanX += deltaX / count;
            meanY += (y[index] - meanY) / count;
            coMoment += deltaX * (y[index] - meanY);
        }

        return coMoment;
    }

    private static void ValidateCovarianceInputs(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        if (x.Length != y.Length) {
            throw new ArgumentException(
                "The samples must have equal length.",
                nameof(y));
        }

        if (x.Length == 0) {
            throw new ArgumentException(
                "At least one paired value is required.",
                nameof(x));
        }

        Validate(x);
        Validate(y);
    }

    private static void ValidateWeightedInputs(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights) {
        if (values.Length != weights.Length) {
            throw new ArgumentException(
                "The values and weights must have equal length.",
                nameof(weights));
        }

        if (values.Length == 0) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        for (var index = 0; index < weights.Length; index++) {
            if (!double.IsFinite(weights[index]) || weights[index] < 0) {
                throw new ArgumentOutOfRangeException(nameof(weights));
            }
        }
    }

    private static double AveragedInverseEmpirical(
        ReadOnlySpan<double> values,
        double position) {
        var lower = Math.Floor(position);
        if (position != lower) {
            return values[(int)Math.Ceiling(position) - 1];
        }

        var index = (int)lower;
        if (index <= 0) {
            return values[0];
        }

        if (index >= values.Length) {
            return values[^1];
        }

        return 0.5 * (values[index - 1] + values[index]);
    }

    private static double InterpolateOneBased(
        ReadOnlySpan<double> values,
        double position) {
        if (position <= 1) {
            return values[0];
        }

        if (position >= values.Length) {
            return values[^1];
        }

        var lowerPosition = Math.Floor(position);
        var lowerIndex = (int)lowerPosition - 1;
        var fraction = position - lowerPosition;
        return values[lowerIndex] +
            (fraction * (values[lowerIndex + 1] - values[lowerIndex]));
    }

    private static void ValidateProbability(double probability) {
        if (!double.IsFinite(probability) ||
            probability < 0 ||
            probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }
    }

    private static void Validate(ReadOnlySpan<double> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "The sample must contain only finite values.",
                    nameof(values));
            }
        }
    }
}
