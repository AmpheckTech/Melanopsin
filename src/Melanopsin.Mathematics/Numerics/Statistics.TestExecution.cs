// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Performs a one-sample Student-t test and reports data and numerical
    /// failure states without changing the established result type.
    /// </summary>
    public static bool TryOneSampleTTest(
        ReadOnlySpan<double> observations,
        double hypothesizedMean,
        out OneSampleTTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.OneSampleT;
        result = default;
        if (observations.Length < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                observations.Length,
                0,
                0);
            return false;
        }

        if (!double.IsFinite(hypothesizedMean) ||
            !ContainsOnlyFinite(observations)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                observations.Length,
                0,
                observations.Length - 1);
            return false;
        }

        var variance = VarianceSample(observations);
        if (!double.IsFinite(variance)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                observations.Length,
                0,
                observations.Length - 1);
            return false;
        }

        if (variance == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                observations.Length,
                0,
                observations.Length - 1);
            return false;
        }

        result = OneSampleTTest(observations, hypothesizedMean);
        if (!double.IsFinite(result.Statistic) ||
            !double.IsFinite(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                observations.Length,
                0,
                observations.Length - 1);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            observations.Length,
            0,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a Welch t test with an explicit termination report.</summary>
    public static bool TryWelchTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out WelchTTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.WelchT;
        result = default;
        if (first.Length < 2 || second.Length < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var firstVariance = VarianceSample(first);
        var secondVariance = VarianceSample(second);
        if (!double.IsFinite(firstVariance) || !double.IsFinite(secondVariance)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var firstComponent = firstVariance / first.Length;
        var secondComponent = secondVariance / second.Length;
        if (firstComponent + secondComponent == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = WelchTTest(first, second);
        if (double.IsNaN(result.Statistic) ||
            double.IsNaN(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var status = firstVariance == 0 || secondVariance == 0
            ? StatisticalTestStatus.SucceededDegenerate
            : StatisticalTestStatus.Succeeded;
        report = CreateTestReport(
            kind,
            status,
            first.Length,
            second.Length,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>
    /// Performs a Pearson chi-square goodness-of-fit test with an explicit
    /// termination report.
    /// </summary>
    public static bool TryChiSquareGoodnessOfFit(
        ReadOnlySpan<double> observedCounts,
        ReadOnlySpan<double> expectedCounts,
        int estimatedParameterCount,
        out ChiSquareGoodnessOfFitResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.ChiSquareGoodnessOfFit;
        result = default;
        if (observedCounts.Length != expectedCounts.Length ||
            observedCounts.Length < 2 ||
            estimatedParameterCount < 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                observedCounts.Length,
                expectedCounts.Length,
                0);
            return false;
        }

        var degreesOfFreedom =
            observedCounts.Length - 1 - estimatedParameterCount;
        if (degreesOfFreedom <= 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                observedCounts.Length,
                expectedCounts.Length,
                0);
            return false;
        }

        var observedTotal = 0.0;
        for (var index = 0; index < observedCounts.Length; index++) {
            var observed = observedCounts[index];
            var expected = expectedCounts[index];
            if (!double.IsFinite(observed) || !double.IsFinite(expected)) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.NonFiniteInput,
                    observedCounts.Length,
                    expectedCounts.Length,
                    degreesOfFreedom);
                return false;
            }

            if (observed < 0 || expected <= 0) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.InvalidData,
                    observedCounts.Length,
                    expectedCounts.Length,
                    degreesOfFreedom);
                return false;
            }

            observedTotal += observed;
        }

        if (!(observedTotal > 0) || !double.IsFinite(observedTotal)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                observedCounts.Length,
                expectedCounts.Length,
                degreesOfFreedom);
            return false;
        }

        result = ChiSquareGoodnessOfFit(
            observedCounts,
            expectedCounts,
            estimatedParameterCount);
        if (!double.IsFinite(result.Statistic) ||
            !double.IsFinite(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                observedCounts.Length,
                expectedCounts.Length,
                degreesOfFreedom);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            observedCounts.Length,
            expectedCounts.Length,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>
    /// Performs a Pearson chi-square independence test with an explicit
    /// termination report.
    /// </summary>
    public static bool TryChiSquareIndependence(
        ReadOnlySpan<int> contingencyTable,
        int rows,
        int columns,
        out ChiSquareIndependenceResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.ChiSquareIndependence;
        result = default;
        if (rows < 2 || columns < 2 ||
            rows > int.MaxValue / columns) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                rows,
                columns,
                0);
            return false;
        }

        var elementCount = rows * columns;
        if (contingencyTable.Length < elementCount) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                rows,
                columns,
                (rows - 1) * (columns - 1));
            return false;
        }

        var rowTotals = new double[rows];
        var columnTotals = new double[columns];
        var total = 0.0;
        for (var row = 0; row < rows; row++) {
            for (var column = 0; column < columns; column++) {
                var count = contingencyTable[(row * columns) + column];
                if (count < 0) {
                    report = CreateTestReport(
                        kind,
                        StatisticalTestStatus.InvalidData,
                        rows,
                        columns,
                        (rows - 1) * (columns - 1));
                    return false;
                }

                rowTotals[row] += count;
                columnTotals[column] += count;
                total += count;
            }
        }

        if (!(total > 0)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                rows,
                columns,
                (rows - 1) * (columns - 1));
            return false;
        }

        for (var row = 0; row < rows; row++) {
            if (rowTotals[row] == 0) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.DegenerateData,
                    rows,
                    columns,
                    (rows - 1) * (columns - 1));
                return false;
            }
        }

        for (var column = 0; column < columns; column++) {
            if (columnTotals[column] == 0) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.DegenerateData,
                    rows,
                    columns,
                    (rows - 1) * (columns - 1));
                return false;
            }
        }

        result = ChiSquareIndependence(contingencyTable, rows, columns);
        if (!double.IsFinite(result.Statistic) ||
            !double.IsFinite(result.TwoSidedPValue) ||
            !double.IsFinite(result.CramersV)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                rows,
                columns,
                (rows - 1) * (columns - 1));
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            rows,
            columns,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a two-sided variance-ratio F test with a report.</summary>
    public static bool TryVarianceRatioFTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out VarianceRatioFTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.VarianceRatioF;
        result = default;
        if (first.Length < 2 || second.Length < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var firstVariance = VarianceSample(first);
        var secondVariance = VarianceSample(second);
        if (!double.IsFinite(firstVariance) || !double.IsFinite(secondVariance)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (firstVariance == 0 && secondVariance == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = VarianceRatioFTest(first, second);
        if (double.IsNaN(result.Statistic) ||
            double.IsNaN(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var status = firstVariance == 0 || secondVariance == 0
            ? StatisticalTestStatus.SucceededDegenerate
            : StatisticalTestStatus.Succeeded;
        report = CreateTestReport(
            kind,
            status,
            first.Length,
            second.Length,
            Math.Min(result.FirstDegreesOfFreedom, result.SecondDegreesOfFreedom));
        return true;
    }

    /// <summary>Performs a paired Student-t test with an explicit report.</summary>
    public static bool TryPairedTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out PairedTTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.PairedT;
        result = default;
        if (first.Length != second.Length) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (first.Length < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                first.Length - 1);
            return false;
        }

        var hasNonZeroDifference = false;
        for (var index = 0; index < first.Length; index++) {
            var difference = first[index] - second[index];
            if (!double.IsFinite(difference)) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.NumericalFailure,
                    first.Length,
                    second.Length,
                    first.Length - 1);
                return false;
            }

            hasNonZeroDifference |= difference != 0;
        }

        if (!hasNonZeroDifference) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                first.Length - 1);
            return false;
        }

        result = PairedTTest(first, second);
        if (!double.IsFinite(result.Statistic) ||
            !double.IsFinite(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                first.Length,
                second.Length,
                first.Length - 1);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            first.Length,
            second.Length,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a Pearson-correlation t test with a report.</summary>
    public static bool TryCorrelationTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out CorrelationTTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.CorrelationT;
        result = default;
        if (first.Length != second.Length) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (first.Length < 3) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                first.Length - 2);
            return false;
        }

        if (VarianceSample(first) == 0 || VarianceSample(second) == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                first.Length - 2);
            return false;
        }

        result = CorrelationTTest(first, second);
        if (double.IsNaN(result.Correlation) ||
            double.IsNaN(result.Statistic) ||
            double.IsNaN(result.TwoSidedPValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NumericalFailure,
                first.Length,
                second.Length,
                first.Length - 2);
            return false;
        }

        var status = Math.Abs(result.Correlation) == 1
            ? StatisticalTestStatus.SucceededDegenerate
            : StatisticalTestStatus.Succeeded;
        report = CreateTestReport(
            kind,
            status,
            first.Length,
            second.Length,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a one-way ANOVA with an explicit termination report.</summary>
    public static bool TryOneWayAnova(
        ReadOnlySpan<double> values,
        ReadOnlySpan<int> groups,
        int groupCount,
        out OneWayAnovaResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.OneWayAnova;
        result = default;
        if (values.Length != groups.Length || values.IsEmpty || groupCount < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                values.Length,
                groupCount,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(values)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                values.Length,
                groupCount,
                0);
            return false;
        }

        var counts = new int[groupCount];
        for (var index = 0; index < groups.Length; index++) {
            var group = groups[index];
            if (group < 0 || group >= groupCount) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.InvalidData,
                    values.Length,
                    groupCount,
                    0);
                return false;
            }

            counts[group]++;
        }

        for (var group = 0; group < groupCount; group++) {
            if (counts[group] == 0) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.DegenerateData,
                    values.Length,
                    groupCount,
                    0);
                return false;
            }
        }

        var withinDegreesOfFreedom = values.Length - groupCount;
        if (withinDegreesOfFreedom == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                values.Length,
                groupCount,
                0);
            return false;
        }

        result = OneWayAnova(values, groups, groupCount);
        if (double.IsNaN(result.Statistic) ||
            double.IsNaN(result.PValue) ||
            double.IsNaN(result.EtaSquared)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                values.Length,
                groupCount,
                withinDegreesOfFreedom);
            return false;
        }

        var status = double.IsPositiveInfinity(result.Statistic)
            ? StatisticalTestStatus.SucceededDegenerate
            : StatisticalTestStatus.Succeeded;
        report = CreateTestReport(
            kind,
            status,
            values.Length,
            groupCount,
            result.WithinGroupDegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a Mann-Whitney U test with a report.</summary>
    public static bool TryMannWhitneyUTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out MannWhitneyUTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.MannWhitneyU;
        result = default;
        if (first.IsEmpty || second.IsEmpty) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = MannWhitneyUTest(first, second);
        if (double.IsNaN(result.StandardizedStatistic) ||
            double.IsNaN(result.PValue) ||
            double.IsNaN(result.RankBiserialEffectSize)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            first.Length,
            second.Length,
            0);
        return true;
    }

    /// <summary>Performs a Wilcoxon signed-rank test with a report.</summary>
    public static bool TryWilcoxonSignedRankTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out WilcoxonSignedRankTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.WilcoxonSignedRank;
        result = default;
        if (first.Length != second.Length) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (first.IsEmpty) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var nonZeroCount = 0;
        for (var index = 0; index < first.Length; index++) {
            var difference = first[index] - second[index];
            if (!double.IsFinite(difference)) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.NumericalFailure,
                    first.Length,
                    second.Length,
                    0);
                return false;
            }

            nonZeroCount += difference == 0 ? 0 : 1;
        }

        if (nonZeroCount == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = WilcoxonSignedRankTest(first, second);
        if (double.IsNaN(result.StandardizedStatistic) ||
            double.IsNaN(result.PValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            first.Length,
            second.Length,
            nonZeroCount);
        return true;
    }

    /// <summary>Performs a Kruskal-Wallis test with a report.</summary>
    public static bool TryKruskalWallisTest(
        ReadOnlySpan<double> values,
        ReadOnlySpan<int> groups,
        int groupCount,
        out KruskalWallisTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.KruskalWallis;
        result = default;
        if (values.Length != groups.Length || values.IsEmpty || groupCount < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                values.Length,
                groupCount,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(values)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                values.Length,
                groupCount,
                groupCount - 1);
            return false;
        }

        var counts = new int[groupCount];
        for (var index = 0; index < groups.Length; index++) {
            var group = groups[index];
            if (group < 0 || group >= groupCount) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.InvalidData,
                    values.Length,
                    groupCount,
                    groupCount - 1);
                return false;
            }

            counts[group]++;
        }

        for (var group = 0; group < groupCount; group++) {
            if (counts[group] == 0) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.DegenerateData,
                    values.Length,
                    groupCount,
                    groupCount - 1);
                return false;
            }
        }

        result = KruskalWallisTest(values, groups, groupCount);
        if (double.IsNaN(result.Statistic) || double.IsNaN(result.PValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                values.Length,
                groupCount,
                groupCount - 1);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            values.Length,
            groupCount,
            result.DegreesOfFreedom);
        return true;
    }

    /// <summary>Performs a Friedman test with a report.</summary>
    public static bool TryFriedmanTest(
        ReadOnlySpan<double> values,
        int blockCount,
        int treatmentCount,
        out FriedmanTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.Friedman;
        result = default;
        if (blockCount < 2 || treatmentCount < 2 ||
            blockCount > int.MaxValue / treatmentCount) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                blockCount,
                treatmentCount,
                0);
            return false;
        }

        var expectedLength = blockCount * treatmentCount;
        if (values.Length != expectedLength) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                blockCount,
                treatmentCount,
                treatmentCount - 1);
            return false;
        }

        if (!ContainsOnlyFinite(values)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                blockCount,
                treatmentCount,
                treatmentCount - 1);
            return false;
        }

        result = FriedmanTest(values, blockCount, treatmentCount);
        if (double.IsNaN(result.Statistic) || double.IsNaN(result.PValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                blockCount,
                treatmentCount,
                treatmentCount - 1);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            blockCount,
            treatmentCount,
            treatmentCount - 1);
        return true;
    }

    /// <summary>Performs an exact paired sign test with a report.</summary>
    public static bool TrySignTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out SignTestResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.Sign;
        result = default;
        if (first.Length != second.Length) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (first.IsEmpty) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        var nonZeroCount = 0;
        for (var index = 0; index < first.Length; index++) {
            var difference = first[index] - second[index];
            if (!double.IsFinite(difference)) {
                report = CreateTestReport(
                    kind,
                    StatisticalTestStatus.NumericalFailure,
                    first.Length,
                    second.Length,
                    0);
                return false;
            }

            nonZeroCount += difference == 0 ? 0 : 1;
        }

        if (nonZeroCount == 0) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = SignTest(first, second);
        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            first.Length,
            second.Length,
            nonZeroCount);
        return true;
    }

    /// <summary>Performs Kendall's tau-b test with a report.</summary>
    public static bool TryKendallTauB(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        out KendallTauResult result,
        out StatisticalTestReport report) {
        const StatisticalTestKind kind = StatisticalTestKind.KendallTauB;
        result = default;
        if (first.Length != second.Length) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InvalidData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (first.Length < 2) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.InsufficientData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.NonFiniteInput,
                first.Length,
                second.Length,
                0);
            return false;
        }

        result = KendallTauB(first, second);
        if (double.IsNaN(result.Coefficient) ||
            double.IsNaN(result.StandardizedStatistic) ||
            double.IsNaN(result.PValue)) {
            result = default;
            report = CreateTestReport(
                kind,
                StatisticalTestStatus.DegenerateData,
                first.Length,
                second.Length,
                0);
            return false;
        }

        report = CreateTestReport(
            kind,
            StatisticalTestStatus.Succeeded,
            first.Length,
            second.Length,
            0);
        return true;
    }

    private static bool ContainsOnlyFinite(ReadOnlySpan<double> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static StatisticalTestReport CreateTestReport(
        StatisticalTestKind test,
        StatisticalTestStatus status,
        int primarySampleSize,
        int secondarySampleSize,
        double degreesOfFreedom) {
        return new StatisticalTestReport(
            test,
            status,
            primarySampleSize,
            secondarySampleSize,
            degreesOfFreedom);
    }
}
