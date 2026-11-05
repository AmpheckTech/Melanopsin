// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The result of a one-way analysis of variance.
/// </summary>
public readonly record struct OneWayAnovaResult(
    double BetweenGroupSumOfSquares,
    double WithinGroupSumOfSquares,
    int BetweenGroupDegreesOfFreedom,
    int WithinGroupDegreesOfFreedom,
    double Statistic,
    double PValue,
    double EtaSquared);

public static partial class Statistics {
    /// <summary>
    /// Performs a one-way ANOVA from flat observations and group indices.
    /// </summary>
    /// <param name="values">Finite observations in any row-major order.</param>
    /// <param name="groups">
    /// Group labels in <c>[0, groupCount)</c>, one for each observation.
    /// </param>
    /// <param name="groupCount">The number of non-empty groups.</param>
    /// <remarks>
    /// The test compares group means assuming independent, approximately
    /// normal observations and equal population variances. If every observation
    /// is identical, the statistic and effect size are undefined and returned
    /// as NaN. If the within-group sum of squares is exactly zero while group
    /// means differ, the statistic is positive infinity and the p-value is
    /// zero.
    /// </remarks>
    public static OneWayAnovaResult OneWayAnova(
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
        var counts = new int[groupCount];
        var means = new double[groupCount];
        var sumSquares = new double[groupCount];
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "Observations must be finite.",
                    nameof(values));
            }

            var group = groups[index];
            if (group < 0 || group >= groupCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(groups),
                    "Every group label must be in range.");
            }

            counts[group]++;
            var difference = values[index] - means[group];
            means[group] += difference / counts[group];
            sumSquares[group] += difference * (values[index] - means[group]);
        }

        for (var group = 0; group < groupCount; group++) {
            if (counts[group] == 0) {
                throw new ArgumentException(
                    "Every group must contain at least one observation.",
                    nameof(groups));
            }
        }

        var totalCount = values.Length;
        var grandMean = 0.0;
        for (var group = 0; group < groupCount; group++) {
            grandMean += counts[group] * means[group];
        }

        grandMean /= totalCount;
        var betweenSumOfSquares = 0.0;
        var withinSumOfSquares = 0.0;
        for (var group = 0; group < groupCount; group++) {
            var difference = means[group] - grandMean;
            betweenSumOfSquares += counts[group] * difference * difference;
            withinSumOfSquares += sumSquares[group];
        }

        var betweenDegreesOfFreedom = groupCount - 1;
        var withinDegreesOfFreedom = totalCount - groupCount;
        var totalSumOfSquares = betweenSumOfSquares + withinSumOfSquares;
        double statistic;
        double pValue;
        if (withinDegreesOfFreedom == 0) {
            statistic = double.NaN;
            pValue = double.NaN;
        } else if (withinSumOfSquares == 0) {
            statistic = betweenSumOfSquares == 0
                ? double.NaN
                : double.PositiveInfinity;
            pValue = betweenSumOfSquares == 0 ? double.NaN : 0;
        } else {
            statistic = (betweenSumOfSquares / betweenDegreesOfFreedom) /
                (withinSumOfSquares / withinDegreesOfFreedom);
            var distribution = new FDistribution(
                betweenDegreesOfFreedom,
                withinDegreesOfFreedom);
            pValue = Math.Clamp(1 - distribution.Cdf(statistic), 0, 1);
        }

        var etaSquared = totalSumOfSquares == 0
            ? double.NaN
            : betweenSumOfSquares / totalSumOfSquares;
        return new OneWayAnovaResult(
            betweenSumOfSquares,
            withinSumOfSquares,
            betweenDegreesOfFreedom,
            withinDegreesOfFreedom,
            statistic,
            pValue,
            etaSquared);
    }
}
