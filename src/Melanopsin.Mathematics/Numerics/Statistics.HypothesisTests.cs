namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Performs a Pearson chi-square goodness-of-fit test against explicitly
    /// supplied expected counts.
    /// </summary>
    /// <remarks>
    /// The counts are treated as fixed observed and expected frequencies, not
    /// as probabilities. Every expected count must be strictly positive. If
    /// parameters were estimated from the same observations, pass their count
    /// through <paramref name="estimatedParameterCount"/> so the reference
    /// chi-square degrees of freedom are reduced accordingly. The asymptotic
    /// p-value is not guaranteed to be accurate when expected counts are small.
    /// </remarks>
    public static ChiSquareGoodnessOfFitResult ChiSquareGoodnessOfFit(
        ReadOnlySpan<double> observedCounts,
        ReadOnlySpan<double> expectedCounts,
        int estimatedParameterCount = 0) {
        if (observedCounts.Length != expectedCounts.Length) {
            throw new ArgumentException(
                "Observed and expected count arrays must have equal lengths.",
                nameof(expectedCounts));
        }

        if (observedCounts.Length < 2) {
            throw new ArgumentException(
                "At least two categories are required.",
                nameof(observedCounts));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(estimatedParameterCount);
        var degreesOfFreedom =
            observedCounts.Length - 1 - estimatedParameterCount;
        if (degreesOfFreedom <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedParameterCount),
                "The estimated parameter count leaves no residual degrees of freedom.");
        }

        var statistic = 0.0;
        var observedTotal = 0.0;
        for (var index = 0; index < observedCounts.Length; index++) {
            var observed = observedCounts[index];
            var expected = expectedCounts[index];
            if (!double.IsFinite(observed) || observed < 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(observedCounts),
                    "Observed counts must be finite and non-negative.");
            }

            if (!double.IsFinite(expected) || expected <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedCounts),
                    "Expected counts must be finite and positive.");
            }

            var difference = observed - expected;
            statistic += (difference * difference) / expected;
            observedTotal += observed;
        }

        if (!(observedTotal > 0) || !double.IsFinite(statistic)) {
            throw new ArgumentOutOfRangeException(
                nameof(observedCounts),
                "The observed counts must have a finite positive total.");
        }

        var pValue = new ChiSquareDistribution(degreesOfFreedom).Survival(
            statistic);
        return new ChiSquareGoodnessOfFitResult(
            statistic,
            degreesOfFreedom,
            pValue);
    }

    /// <summary>
    /// Performs a Pearson chi-square test of independence on a row-major
    /// contingency table.
    /// </summary>
    /// <remarks>
    /// Row and column marginal totals must all be positive. The returned
    /// Cramer's V uses the uncorrected chi-square statistic and has the usual
    /// zero-to-one range for a contingency table.
    /// </remarks>
    public static ChiSquareIndependenceResult ChiSquareIndependence(
        ReadOnlySpan<int> contingencyTable,
        int rows,
        int columns) {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        var elementCount = checked(rows * columns);
        if (contingencyTable.Length < elementCount) {
            throw new ArgumentException(
                "The contingency table does not contain enough values.",
                nameof(contingencyTable));
        }

        var rowTotals = new double[rows];
        var columnTotals = new double[columns];
        var total = 0.0;
        for (var row = 0; row < rows; row++) {
            for (var column = 0; column < columns; column++) {
                var count = contingencyTable[(row * columns) + column];
                ArgumentOutOfRangeException.ThrowIfNegative(count);
                rowTotals[row] += count;
                columnTotals[column] += count;
                total += count;
            }
        }

        if (!(total > 0)) {
            throw new ArgumentOutOfRangeException(
                nameof(contingencyTable),
                "The contingency table must have a positive total.");
        }

        for (var row = 0; row < rows; row++) {
            if (rowTotals[row] == 0) {
                throw new ArgumentException(
                    "Every table row must have a positive total.",
                    nameof(contingencyTable));
            }
        }

        for (var column = 0; column < columns; column++) {
            if (columnTotals[column] == 0) {
                throw new ArgumentException(
                    "Every table column must have a positive total.",
                    nameof(contingencyTable));
            }
        }

        var statistic = 0.0;
        for (var row = 0; row < rows; row++) {
            for (var column = 0; column < columns; column++) {
                var observed = contingencyTable[(row * columns) + column];
                var expected = rowTotals[row] * columnTotals[column] / total;
                var difference = observed - expected;
                statistic += (difference * difference) / expected;
            }
        }

        var degreesOfFreedom = (rows - 1) * (columns - 1);
        var pValue = new ChiSquareDistribution(degreesOfFreedom).Survival(
            statistic);
        var cramersV = Math.Sqrt(
            statistic / (total * Math.Min(rows - 1, columns - 1)));
        return new ChiSquareIndependenceResult(
            statistic,
            degreesOfFreedom,
            pValue,
            Math.Clamp(cramersV, 0, 1));
    }

    /// <summary>
    /// Performs a two-sided F test for equality of two population variances.
    /// </summary>
    /// <remarks>
    /// The statistic is the ratio of the first sample variance to the second.
    /// A two-sided p-value is formed from twice the smaller F tail and clamped
    /// to one. The test assumes independent samples from normal populations.
    /// It does not use an absolute variance threshold and therefore remains
    /// scale invariant.
    /// </remarks>
    public static VarianceRatioFTestResult VarianceRatioFTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateTwoSamples(first, second);
        var firstVariance = VarianceSample(first);
        var secondVariance = VarianceSample(second);
        var firstDegreesOfFreedom = first.Length - 1;
        var secondDegreesOfFreedom = second.Length - 1;
        if (secondVariance == 0) {
            var statistic = firstVariance == 0
                ? double.NaN
                : double.PositiveInfinity;
            return new VarianceRatioFTestResult(
                firstVariance,
                secondVariance,
                statistic,
                firstDegreesOfFreedom,
                secondDegreesOfFreedom,
                double.IsNaN(statistic) ? double.NaN : 0);
        }

        var ratio = firstVariance / secondVariance;
        var distribution = new FDistribution(
            firstDegreesOfFreedom,
            secondDegreesOfFreedom);
        var lowerTail = distribution.Cdf(ratio);
        var upperTail = 1 - lowerTail;
        var pValue = Math.Clamp(2 * Math.Min(lowerTail, upperTail), 0, 1);
        return new VarianceRatioFTestResult(
            firstVariance,
            secondVariance,
            ratio,
            firstDegreesOfFreedom,
            secondDegreesOfFreedom,
            pValue);
    }

    /// <summary>
    /// Performs a two-sided paired Student-t test on matched observations.
    /// </summary>
    public static PairedTTestResult PairedTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        if (first.Length != second.Length) {
            throw new ArgumentException(
                "Paired samples must have equal lengths.",
                nameof(second));
        }

        if (first.Length < 2) {
            throw new ArgumentException(
                "At least two pairs are required.",
                nameof(first));
        }

        var differences = new double[first.Length];
        for (var index = 0; index < first.Length; index++) {
            differences[index] = first[index] - second[index];
        }

        var test = OneSampleTTest(differences);
        return new PairedTTestResult(
            Mean(differences),
            test.Statistic,
            test.DegreesOfFreedom,
            test.TwoSidedPValue);
    }

    /// <summary>
    /// Tests whether a Pearson correlation differs from zero using the
    /// Student-t transformation of the sample correlation coefficient.
    /// </summary>
    /// <remarks>
    /// At least three paired observations are required. Perfect correlation
    /// produces an infinite statistic and a zero two-sided p-value. A constant
    /// input has no defined Pearson correlation and returns NaN fields.
    /// </remarks>
    public static CorrelationTTestResult CorrelationTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateCovarianceInputs(first, second);
        if (first.Length < 3) {
            throw new ArgumentException(
                "At least three paired observations are required.",
                nameof(first));
        }

        var correlation = Correlation(first, second);
        var degreesOfFreedom = first.Length - 2;
        if (double.IsNaN(correlation)) {
            return new CorrelationTTestResult(
                correlation,
                double.NaN,
                degreesOfFreedom,
                double.NaN);
        }

        if (Math.Abs(correlation) == 1) {
            return new CorrelationTTestResult(
                correlation,
                Math.CopySign(double.PositiveInfinity, correlation),
                degreesOfFreedom,
                0);
        }

        var statistic = correlation * Math.Sqrt(
            degreesOfFreedom / (1 - (correlation * correlation)));
        var pValue = TwoSidedStudentTPValue(statistic, degreesOfFreedom);
        return new CorrelationTTestResult(
            correlation,
            statistic,
            degreesOfFreedom,
            pValue);
    }
}

/// <summary>
/// The result of a chi-square goodness-of-fit test.
/// </summary>
public readonly record struct ChiSquareGoodnessOfFitResult(
    double Statistic,
    int DegreesOfFreedom,
    double TwoSidedPValue);

/// <summary>
/// The result of a chi-square test of independence.
/// </summary>
public readonly record struct ChiSquareIndependenceResult(
    double Statistic,
    int DegreesOfFreedom,
    double TwoSidedPValue,
    double CramersV);

/// <summary>
/// The result of a two-sided variance-ratio F test.
/// </summary>
public readonly record struct VarianceRatioFTestResult(
    double FirstSampleVariance,
    double SecondSampleVariance,
    double Statistic,
    int FirstDegreesOfFreedom,
    int SecondDegreesOfFreedom,
    double TwoSidedPValue);

/// <summary>
/// The result of a two-sided paired Student-t test.
/// </summary>
public readonly record struct PairedTTestResult(
    double MeanDifference,
    double Statistic,
    double DegreesOfFreedom,
    double TwoSidedPValue);

/// <summary>
/// The result of a two-sided test for zero Pearson correlation.
/// </summary>
public readonly record struct CorrelationTTestResult(
    double Correlation,
    double Statistic,
    double DegreesOfFreedom,
    double TwoSidedPValue);
