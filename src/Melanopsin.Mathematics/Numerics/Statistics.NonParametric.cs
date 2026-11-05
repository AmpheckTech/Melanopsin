// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The result of an asymptotic Mann-Whitney U test.
/// </summary>
public readonly record struct MannWhitneyUTestResult(
    double U,
    double StandardizedStatistic,
    double PValue,
    double RankBiserialEffectSize,
    int FirstSampleSize,
    int SecondSampleSize);

/// <summary>
/// The result of an asymptotic Wilcoxon signed-rank test.
/// </summary>
public readonly record struct WilcoxonSignedRankTestResult(
    double PositiveRankSum,
    double NegativeRankSum,
    double StandardizedStatistic,
    double PValue,
    double RankBiserialEffectSize,
    int NonZeroPairCount);

/// <summary>
/// The result of a Kruskal-Wallis one-way rank test.
/// </summary>
public readonly record struct KruskalWallisTestResult(
    double Statistic,
    int DegreesOfFreedom,
    double PValue,
    double TieCorrection);

/// <summary>
/// The result of a Friedman rank test.
/// </summary>
public readonly record struct FriedmanTestResult(
    double Statistic,
    int TreatmentCount,
    int BlockCount,
    double PValue,
    double TieCorrection);

/// <summary>
/// The result of an exact two-sided paired sign test.
/// </summary>
public readonly record struct SignTestResult(
    int PositiveCount,
    int NegativeCount,
    double PValue);

/// <summary>
/// The result of Kendall's tau-b rank correlation.
/// </summary>
public readonly record struct KendallTauResult(
    double Coefficient,
    double StandardizedStatistic,
    double PValue,
    int Count);

public static partial class Statistics {
    /// <summary>
    /// Performs a two-sided Mann-Whitney U rank-sum test.
    /// </summary>
    /// <remarks>
    /// The p-value uses the normal approximation with a tie-corrected
    /// variance and a continuity correction. It is not an exact permutation
    /// p-value and can be inaccurate for very small or heavily tied samples.
    /// Exact equality is used to identify ties. The reported U is the statistic
    /// for <paramref name="first"/>; its expected value under the null
    /// hypothesis is <c>n1 * n2 / 2</c>. Reference: Mann and Whitney (1947),
    /// <see href="https://doi.org/10.1214/aoms/1177730491">doi:10.1214/aoms/1177730491</see>.
    /// </remarks>
    public static MannWhitneyUTestResult MannWhitneyUTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        if (first.IsEmpty || second.IsEmpty) {
            throw new ArgumentException(
                "Both samples must contain at least one value.");
        }

        Validate(first);
        Validate(second);
        var combinedLength = checked(first.Length + second.Length);
        var combined = new double[combinedLength];
        first.CopyTo(combined);
        second.CopyTo(combined.AsSpan(first.Length));
        var ranks = new double[combinedLength];
        var tieCorrection = AssignAverageRanks(combined, ranks);

        var firstRankSum = 0.0;
        for (var index = 0; index < first.Length; index++) {
            firstRankSum += ranks[index];
        }

        var firstCount = first.Length;
        var secondCount = second.Length;
        var product = (double)firstCount * secondCount;
        var statistic = firstRankSum - (0.5 * firstCount * (firstCount + 1.0));
        var mean = 0.5 * product;
        var correction = 1 - (tieCorrection /
            ((double)combinedLength * combinedLength * combinedLength - combinedLength));
        var variance = product * (combinedLength + 1.0) * correction / 12.0;
        if (!(variance > 0) || !double.IsFinite(variance)) {
            return new MannWhitneyUTestResult(
                statistic,
                double.NaN,
                double.NaN,
                EffectSizeFromU(statistic, product),
                firstCount,
                secondCount);
        }

        var difference = statistic - mean;
        var directionalCorrection = difference == 0 ? 0 : 0.5 * Math.Sign(difference);
        var standardized = (difference - directionalCorrection) /
            Math.Sqrt(variance);
        var pValue = Math.Clamp(
            SpecialFunctions.Erfc(Math.Abs(standardized) * Constants.InverseSqrt2),
            0,
            1);
        return new MannWhitneyUTestResult(
            statistic,
            standardized,
            pValue,
            EffectSizeFromU(statistic, product),
            firstCount,
            secondCount);
    }

    /// <summary>
    /// Performs a two-sided Wilcoxon signed-rank test for paired samples.
    /// </summary>
    /// <remarks>
    /// Exact zero pair differences are discarded. Absolute differences are
    /// assigned average ranks, so tied magnitudes are supported. The p-value
    /// uses the normal approximation with tie correction and continuity
    /// correction; it is not an exact signed-rank permutation p-value. If
    /// every non-zero magnitude has the same rank and the asymptotic variance
    /// is zero, the standardized statistic and p-value are NaN. Reference:
    /// Wilcoxon (1945),
    /// <see href="https://doi.org/10.2307/3001968">doi:10.2307/3001968</see>.
    /// </remarks>
    public static WilcoxonSignedRankTestResult WilcoxonSignedRankTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        if (first.Length != second.Length) {
            throw new ArgumentException(
                "The paired samples must have equal length.",
                nameof(second));
        }

        if (first.IsEmpty) {
            throw new ArgumentException(
                "At least one pair is required.",
                nameof(first));
        }

        Validate(first);
        Validate(second);
        var differences = new double[first.Length];
        var nonZeroCount = 0;
        for (var index = 0; index < first.Length; index++) {
            var difference = first[index] - second[index];
            if (!double.IsFinite(difference)) {
                throw new ArithmeticException(
                    "A paired difference is not representable as a finite value.");
            }

            if (difference != 0) {
                differences[nonZeroCount++] = difference;
            }
        }

        if (nonZeroCount == 0) {
            throw new ArgumentException(
                "The signed-rank test requires at least one non-zero difference.",
                nameof(first));
        }

        var magnitudes = new double[nonZeroCount];
        for (var index = 0; index < nonZeroCount; index++) {
            magnitudes[index] = Math.Abs(differences[index]);
        }

        var ranks = new double[nonZeroCount];
        var tieCorrection = AssignAverageRanks(magnitudes, ranks);
        var positiveRankSum = 0.0;
        var totalRankSum = 0.0;
        for (var index = 0; index < nonZeroCount; index++) {
            totalRankSum += ranks[index];
            if (differences[index] > 0) {
                positiveRankSum += ranks[index];
            }
        }

        var negativeRankSum = totalRankSum - positiveRankSum;
        var mean = nonZeroCount * (nonZeroCount + 1.0) / 4.0;
        var variance =
            (nonZeroCount * (nonZeroCount + 1.0) * (2.0 * nonZeroCount + 1.0) / 24.0) -
            (tieCorrection / 48.0);
        var effectSize = totalRankSum == 0
            ? double.NaN
            : (positiveRankSum - negativeRankSum) / totalRankSum;
        if (!(variance > 0) || !double.IsFinite(variance)) {
            return new WilcoxonSignedRankTestResult(
                positiveRankSum,
                negativeRankSum,
                double.NaN,
                double.NaN,
                effectSize,
                nonZeroCount);
        }

        var centeredDifference = positiveRankSum - mean;
        var directionalCorrection = centeredDifference == 0
            ? 0
            : 0.5 * Math.Sign(centeredDifference);
        var standardized = (centeredDifference - directionalCorrection) /
            Math.Sqrt(variance);
        var pValue = Math.Clamp(
            SpecialFunctions.Erfc(Math.Abs(standardized) * Constants.InverseSqrt2),
            0,
            1);
        return new WilcoxonSignedRankTestResult(
            positiveRankSum,
            negativeRankSum,
            standardized,
            pValue,
            effectSize,
            nonZeroCount);
    }

    /// <summary>
    /// Performs a Kruskal-Wallis one-way analysis of variance by ranks.
    /// </summary>
    /// <param name="values">Finite observations in any row-major order.</param>
    /// <param name="groups">
    /// Group labels in <c>[0, groupCount)</c>, one for each observation.
    /// </param>
    /// <param name="groupCount">The number of non-empty groups.</param>
    /// <remarks>
    /// The chi-square p-value is asymptotic and uses the standard tie
    /// correction. If every observation is tied, the corrected statistic and
    /// p-value are undefined and returned as NaN. No exact small-sample
    /// p-value is provided. Reference: Kruskal and Wallis (1952),
    /// <see href="https://doi.org/10.1080/01621459.1952.10483441">doi:10.1080/01621459.1952.10483441</see>.
    /// </remarks>
    public static KruskalWallisTestResult KruskalWallisTest(
        ReadOnlySpan<double> values,
        ReadOnlySpan<int> groups,
        int groupCount) {
        if (values.Length != groups.Length) {
            throw new ArgumentException(
                "The observations and group labels must have equal length.",
                nameof(groups));
        }

        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one observation is required.",
                nameof(values));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(groupCount, 2);
        Validate(values);
        var counts = new int[groupCount];
        for (var index = 0; index < groups.Length; index++) {
            var group = groups[index];
            if (group < 0 || group >= groupCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(groups),
                    "Every group label must be in range.");
            }

            counts[group]++;
        }

        for (var group = 0; group < groupCount; group++) {
            if (counts[group] == 0) {
                throw new ArgumentException(
                    "Every group must contain at least one observation.",
                    nameof(groups));
            }
        }

        var ranks = new double[values.Length];
        var tieCorrectionSum = AssignAverageRanks(values, ranks);
        var rankSums = new double[groupCount];
        for (var index = 0; index < values.Length; index++) {
            rankSums[groups[index]] += ranks[index];
        }

        var count = values.Length;
        var statistic = 0.0;
        for (var group = 0; group < groupCount; group++) {
            statistic += rankSums[group] * rankSums[group] / counts[group];
        }

        statistic = (12.0 / (count * (count + 1.0))) * statistic -
            3.0 * (count + 1.0);
        var uncorrectedDenominator = (double)count * count * count - count;
        var tieCorrection = uncorrectedDenominator == 0
            ? 0
            : 1 - (tieCorrectionSum / uncorrectedDenominator);
        var degreesOfFreedom = groupCount - 1;
        if (!(tieCorrection > 0) || !double.IsFinite(statistic)) {
            return new KruskalWallisTestResult(
                double.NaN,
                degreesOfFreedom,
                double.NaN,
                tieCorrection);
        }

        statistic /= tieCorrection;
        var pValue = new ChiSquareDistribution(degreesOfFreedom).Survival(
            Math.Max(0, statistic));
        return new KruskalWallisTestResult(
            statistic,
            degreesOfFreedom,
            Math.Clamp(pValue, 0, 1),
            tieCorrection);
    }

    /// <summary>
    /// Performs the Friedman test for a row-major block-by-treatment matrix.
    /// </summary>
    /// <remarks>
    /// Each row is one block and each column is one treatment. Values within
    /// every block are replaced by average ranks. The chi-square p-value is
    /// asymptotic and uses the standard tie correction. At least two blocks
    /// and two treatments are required. If every value within every block is
    /// tied, the statistic and p-value are returned as NaN. Reference:
    /// Friedman (1937),
    /// <see href="https://doi.org/10.1080/01621459.1937.10503522">doi:10.1080/01621459.1937.10503522</see>.
    /// </remarks>
    public static FriedmanTestResult FriedmanTest(
        ReadOnlySpan<double> values,
        int blockCount,
        int treatmentCount) {
        ArgumentOutOfRangeException.ThrowIfLessThan(blockCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(treatmentCount, 2);
        var expectedLength = checked(blockCount * treatmentCount);
        if (values.Length != expectedLength) {
            throw new ArgumentException(
                "The value count must equal blockCount * treatmentCount.",
                nameof(values));
        }

        Validate(values);
        var rankSums = new double[treatmentCount];
        var block = new double[treatmentCount];
        var ranks = new double[treatmentCount];
        var tieCorrectionSum = 0.0;
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++) {
            values.Slice(blockIndex * treatmentCount, treatmentCount).CopyTo(block);
            tieCorrectionSum += AssignAverageRanks(block, ranks);
            for (var treatment = 0; treatment < treatmentCount; treatment++) {
                rankSums[treatment] += ranks[treatment];
            }
        }

        var statistic = 0.0;
        for (var treatment = 0; treatment < treatmentCount; treatment++) {
            statistic += rankSums[treatment] * rankSums[treatment];
        }

        statistic *= 12.0 /
            (blockCount * (double)treatmentCount * (treatmentCount + 1.0));
        statistic -= 3.0 * blockCount * (treatmentCount + 1.0);
        var tieDenominator = blockCount *
            ((double)treatmentCount * treatmentCount * treatmentCount - treatmentCount);
        var tieCorrection = tieDenominator == 0
            ? 0
            : 1 - (tieCorrectionSum / tieDenominator);
        var degreesOfFreedom = treatmentCount - 1;
        if (!(tieCorrection > 0) || !double.IsFinite(statistic)) {
            return new FriedmanTestResult(
                double.NaN,
                treatmentCount,
                blockCount,
                double.NaN,
                tieCorrection);
        }

        statistic /= tieCorrection;
        var pValue = new ChiSquareDistribution(degreesOfFreedom).Survival(
            Math.Max(0, statistic));
        return new FriedmanTestResult(
            statistic,
            treatmentCount,
            blockCount,
            Math.Clamp(pValue, 0, 1),
            tieCorrection);
    }

    /// <summary>
    /// Performs an exact two-sided sign test for paired samples.
    /// </summary>
    /// <remarks>
    /// Exact zero differences are ignored. Under the null hypothesis the
    /// number of positive differences among the remaining pairs is binomial
    /// with success probability one half. The p-value is the exact central
    /// two-sided binomial probability and is not continuity corrected.
    /// </remarks>
    public static SignTestResult SignTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        if (first.Length != second.Length) {
            throw new ArgumentException(
                "The paired samples must have equal length.",
                nameof(second));
        }

        if (first.IsEmpty) {
            throw new ArgumentException(
                "At least one pair is required.",
                nameof(first));
        }

        Validate(first);
        Validate(second);
        var positiveCount = 0;
        var negativeCount = 0;
        for (var index = 0; index < first.Length; index++) {
            var difference = first[index] - second[index];
            if (difference > 0) {
                positiveCount++;
            } else if (difference < 0) {
                negativeCount++;
            }
        }

        var count = positiveCount + negativeCount;
        if (count == 0) {
            throw new ArgumentException(
                "The sign test requires at least one non-zero difference.",
                nameof(first));
        }

        var tailCount = Math.Min(positiveCount, negativeCount);
        var tailProbability = new BinomialDistribution(count, 0.5).Cdf(tailCount);
        return new SignTestResult(
            positiveCount,
            negativeCount,
            Math.Clamp(2 * tailProbability, 0, 1));
    }

    /// <summary>
    /// Computes Spearman rank correlation with average ranks for ties.
    /// </summary>
    /// <remarks>
    /// This is the Pearson correlation of the two average-rank vectors. It is
    /// descriptive and does not attach a p-value. If either sample is
    /// constant, correlation is undefined and NaN is returned.
    /// </remarks>
    public static double SpearmanRankCorrelation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateCovarianceInputs(first, second);
        var firstRanks = new double[first.Length];
        var secondRanks = new double[second.Length];
        AssignAverageRanks(first, firstRanks);
        AssignAverageRanks(second, secondRanks);
        var mean = 0.5 * (first.Length + 1.0);
        var firstSumSquares = 0.0;
        var secondSumSquares = 0.0;
        var crossProduct = 0.0;
        for (var index = 0; index < first.Length; index++) {
            var firstDifference = firstRanks[index] - mean;
            var secondDifference = secondRanks[index] - mean;
            firstSumSquares += firstDifference * firstDifference;
            secondSumSquares += secondDifference * secondDifference;
            crossProduct += firstDifference * secondDifference;
        }

        if (firstSumSquares == 0 || secondSumSquares == 0) {
            return double.NaN;
        }

        return crossProduct / Math.Sqrt(firstSumSquares * secondSumSquares);
    }

    /// <summary>
    /// Computes Kendall's tau-b correlation with an asymptotic two-sided
    /// p-value.
    /// </summary>
    /// <remarks>
    /// Tau-b explicitly adjusts for ties in either or both variables. The
    /// p-value uses the normal approximation under the null hypothesis and is
    /// not exact for small or heavily tied samples. If all observations in
    /// either variable are tied, the coefficient and p-value are NaN.
    /// Reference for the tau-b coefficient: Kendall (1938),
    /// <see href="https://doi.org/10.1093/biomet/30.1-2.81">doi:10.1093/biomet/30.1-2.81</see>.
    /// </remarks>
    public static KendallTauResult KendallTauB(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateCovarianceInputs(first, second);
        if (first.Length < 2) {
            throw new ArgumentException(
                "At least two paired values are required.",
                nameof(first));
        }

        var concordant = 0L;
        var discordant = 0L;
        var tiedFirst = 0L;
        var tiedSecond = 0L;
        for (var firstIndex = 0; firstIndex < first.Length - 1; firstIndex++) {
            for (var secondIndex = firstIndex + 1; secondIndex < first.Length; secondIndex++) {
                var firstDifference = first[firstIndex] - first[secondIndex];
                var secondDifference = second[firstIndex] - second[secondIndex];
                if (firstDifference == 0) {
                    tiedFirst++;
                }

                if (secondDifference == 0) {
                    tiedSecond++;
                }

                if (firstDifference == 0 || secondDifference == 0) {
                    continue;
                }

                if ((firstDifference > 0) == (secondDifference > 0)) {
                    concordant++;
                } else {
                    discordant++;
                }
            }
        }

        var pairCount = (double)first.Length * (first.Length - 1) / 2;
        var denominator = Math.Sqrt(
            (pairCount - tiedFirst) * (pairCount - tiedSecond));
        if (!(denominator > 0)) {
            return new KendallTauResult(
                double.NaN,
                double.NaN,
                double.NaN,
                first.Length);
        }

        var coefficient = (concordant - discordant) / denominator;
        var nullVariance = 2.0 * (2.0 * first.Length + 5.0) /
            (9.0 * first.Length * (first.Length - 1.0));
        if (!(nullVariance > 0)) {
            return new KendallTauResult(
                coefficient,
                double.NaN,
                double.NaN,
                first.Length);
        }

        var standardized = coefficient / Math.Sqrt(nullVariance);
        var pValue = Math.Clamp(
            SpecialFunctions.Erfc(Math.Abs(standardized) * Constants.InverseSqrt2),
            0,
            1);
        return new KendallTauResult(
            coefficient,
            standardized,
            pValue,
            first.Length);
    }

    private static double AssignAverageRanks(
        ReadOnlySpan<double> values,
        Span<double> ranks) {
        if (values.Length != ranks.Length) {
            throw new ArgumentException(
                "The rank destination must have the same length as the values.",
                nameof(ranks));
        }

        var keys = values.ToArray();
        var order = new int[values.Length];
        for (var index = 0; index < order.Length; index++) {
            order[index] = index;
        }

        Array.Sort(keys, order);
        var tieCorrection = 0.0;
        var start = 0;
        while (start < keys.Length) {
            var end = start + 1;
            while (end < keys.Length && keys[end] == keys[start]) {
                end++;
            }

            var tieCount = end - start;
            var averageRank = 0.5 * (start + end + 1.0);
            for (var index = start; index < end; index++) {
                ranks[order[index]] = averageRank;
            }

            if (tieCount > 1) {
                tieCorrection += (double)tieCount * tieCount * tieCount - tieCount;
            }

            start = end;
        }

        return tieCorrection;
    }

    private static double EffectSizeFromU(double statistic, double product) {
        return product == 0 ? double.NaN : (2 * statistic / product) - 1;
    }
}
