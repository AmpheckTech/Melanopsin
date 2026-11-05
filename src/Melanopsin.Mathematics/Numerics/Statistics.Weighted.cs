namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Computes the population variance of a weighted sample.
    /// </summary>
    /// <remarks>
    /// Weights are finite, non-negative relative weights. The result divides
    /// the weighted sum of squared deviations by the total weight. A positive
    /// total weight is required.
    /// </remarks>
    public static double WeightedVariancePopulation(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights) {
        return ComputeWeightedMoments(values, weights).PopulationVariance;
    }

    /// <summary>
    /// Computes the reliability-weighted unbiased sample variance.
    /// </summary>
    /// <remarks>
    /// The denominator is <c>totalWeight - sum(weight^2) / totalWeight</c>.
    /// For equal weights this reduces to the ordinary <c>n - 1</c>
    /// denominator. At least two positive-weight observations are required.
    /// Frequency weights should be expanded or a frequency-specific estimator
    /// should be used instead.
    /// </remarks>
    public static double WeightedVarianceSample(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights) {
        var moments = ComputeWeightedMoments(values, weights);
        var denominator = moments.TotalWeight -
            (moments.SumSquaredWeights / moments.TotalWeight);
        if (moments.PositiveWeightCount < 2 || !(denominator > 0)) {
            throw new ArgumentException(
                "At least two positive-weight observations are required.",
                nameof(weights));
        }

        return moments.WeightedSumSquares / denominator;
    }

    /// <summary>
    /// Computes the population covariance of two weighted samples.
    /// </summary>
    public static double WeightedCovariancePopulation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        ReadOnlySpan<double> weights) {
        return ComputeWeightedCovariance(first, second, weights)
            .PopulationCovariance;
    }

    /// <summary>
    /// Computes the reliability-weighted unbiased sample covariance.
    /// </summary>
    /// <remarks>
    /// The denominator matches <see cref="WeightedVarianceSample"/>.
    /// </remarks>
    public static double WeightedCovarianceSample(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        ReadOnlySpan<double> weights) {
        var moments = ComputeWeightedCovariance(first, second, weights);
        var denominator = moments.TotalWeight -
            (moments.SumSquaredWeights / moments.TotalWeight);
        if (moments.PositiveWeightCount < 2 || !(denominator > 0)) {
            throw new ArgumentException(
                "At least two positive-weight observations are required.",
                nameof(weights));
        }

        return moments.WeightedCoMoment / denominator;
    }

    /// <summary>
    /// Computes the weighted Pearson correlation using population
    /// normalization.
    /// </summary>
    /// <remarks>
    /// The normalization cancels between covariance and variances, so this
    /// value is the same for population and reliability sample conventions.
    /// It returns NaN when either weighted variance is exactly zero.
    /// </remarks>
    public static double WeightedCorrelation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        ReadOnlySpan<double> weights) {
        var covariance = ComputeWeightedCovariance(first, second, weights);
        var firstVariance = ComputeWeightedMoments(first, weights)
            .PopulationVariance;
        var secondVariance = ComputeWeightedMoments(second, weights)
            .PopulationVariance;
        if (firstVariance == 0 || secondVariance == 0) {
            return double.NaN;
        }

        return covariance.PopulationCovariance /
            Math.Sqrt(firstVariance * secondVariance);
    }

    /// <summary>
    /// Computes <c>log(sum(exp(values)))</c> without avoidable overflow or
    /// underflow.
    /// </summary>
    /// <remarks>
    /// An empty input returns negative infinity. A positive infinity dominates
    /// and returns positive infinity. NaN propagates. If all values are
    /// negative infinity the result is negative infinity.
    /// </remarks>
    public static double LogSumExp(ReadOnlySpan<double> values) {
        if (values.IsEmpty) {
            return double.NegativeInfinity;
        }

        var maximum = double.NegativeInfinity;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            if (double.IsNaN(value)) {
                return double.NaN;
            }

            if (value == double.PositiveInfinity) {
                return double.PositiveInfinity;
            }

            maximum = Math.Max(maximum, value);
        }

        if (maximum == double.NegativeInfinity) {
            return double.NegativeInfinity;
        }

        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            sum += Math.Exp(values[index] - maximum);
        }

        return maximum + Math.Log(sum);
    }

    private static WeightedMoments ComputeWeightedMoments(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights) {
        ValidateWeightedInputs(values, weights);
        var mean = 0.0;
        var totalWeight = 0.0;
        var sumSquaredWeights = 0.0;
        var weightedSumSquares = 0.0;
        var positiveWeightCount = 0;
        for (var index = 0; index < values.Length; index++) {
            var weight = weights[index];
            if (weight == 0) {
                continue;
            }

            positiveWeightCount++;
            var nextWeight = totalWeight + weight;
            var difference = values[index] - mean;
            mean += difference * weight / nextWeight;
            weightedSumSquares += weight * difference * (values[index] - mean);
            totalWeight = nextWeight;
            sumSquaredWeights += weight * weight;
        }

        if (!(totalWeight > 0) || !double.IsFinite(totalWeight)) {
            throw new ArgumentException(
                "The sum of weights must be finite and positive.",
                nameof(weights));
        }

        if (!(weightedSumSquares >= 0) || !double.IsFinite(weightedSumSquares)) {
            throw new ArithmeticException(
                "The weighted variance could not be computed with finite arithmetic.");
        }

        return new WeightedMoments(
            mean,
            totalWeight,
            sumSquaredWeights,
            weightedSumSquares,
            positiveWeightCount);
    }

    private static WeightedCovarianceMoments ComputeWeightedCovariance(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        ReadOnlySpan<double> weights) {
        if (first.Length != second.Length) {
            throw new ArgumentException(
                "The samples must have equal length.",
                nameof(second));
        }

        ValidateWeightedInputs(first, weights);
        ValidateFiniteValues(second, nameof(second));
        if (second.Length != weights.Length) {
            throw new ArgumentException(
                "The samples and weights must have equal length.",
                nameof(weights));
        }

        var meanFirst = 0.0;
        var meanSecond = 0.0;
        var totalWeight = 0.0;
        var sumSquaredWeights = 0.0;
        var weightedCoMoment = 0.0;
        var positiveWeightCount = 0;
        for (var index = 0; index < first.Length; index++) {
            var weight = weights[index];
            if (weight == 0) {
                continue;
            }

            positiveWeightCount++;
            var nextWeight = totalWeight + weight;
            var firstDifference = first[index] - meanFirst;
            meanFirst += firstDifference * weight / nextWeight;
            meanSecond += (second[index] - meanSecond) * weight / nextWeight;
            weightedCoMoment += weight *
                firstDifference *
                (second[index] - meanSecond);
            totalWeight = nextWeight;
            sumSquaredWeights += weight * weight;
        }

        if (!(totalWeight > 0) || !double.IsFinite(totalWeight)) {
            throw new ArgumentException(
                "The sum of weights must be finite and positive.",
                nameof(weights));
        }

        if (!double.IsFinite(weightedCoMoment)) {
            throw new ArithmeticException(
                "The weighted covariance could not be computed with finite arithmetic.");
        }

        return new WeightedCovarianceMoments(
            totalWeight,
            sumSquaredWeights,
            weightedCoMoment,
            positiveWeightCount);
    }

    private static void ValidateFiniteValues(
        ReadOnlySpan<double> values,
        string parameterName) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "The sample must contain only finite values.",
                    parameterName);
            }
        }
    }

    private readonly record struct WeightedMoments(
        double Mean,
        double TotalWeight,
        double SumSquaredWeights,
        double WeightedSumSquares,
        int PositiveWeightCount) {
        public double PopulationVariance => WeightedSumSquares / TotalWeight;
    }

    private readonly record struct WeightedCovarianceMoments(
        double TotalWeight,
        double SumSquaredWeights,
        double WeightedCoMoment,
        int PositiveWeightCount) {
        public double PopulationCovariance => WeightedCoMoment / TotalWeight;
    }
}
