// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies a probability or statistical operation.</summary>
public enum DistributionOperation {
    /// <summary>Evaluation of a probability density or mass function.</summary>
    ProbabilityFunction,

    /// <summary>Evaluation of a log probability density or mass function.</summary>
    LogProbabilityFunction,

    /// <summary>Drawing one sample from a probability distribution.</summary>
    Sampling,

    /// <summary>Factorization of a distribution scale or observation matrix.</summary>
    MatrixFactorization,

    /// <summary>Solution of a triangular system inside a distribution calculation.</summary>
    MatrixSolve,

    /// <summary>Inversion of a positive-definite distribution matrix.</summary>
    MatrixInversion,

    /// <summary>Evaluation of a distribution mean or mean matrix.</summary>
    Mean,

    /// <summary>Evaluation of a cumulative distribution function.</summary>
    CumulativeDistribution,

    /// <summary>Evaluation of a quantile function.</summary>
    Quantile,

    /// <summary>Maximum-likelihood fitting of a normal distribution.</summary>
    NormalMaximumLikelihood,

    /// <summary>Maximum-likelihood fitting of an exponential distribution.</summary>
    ExponentialMaximumLikelihood,

    /// <summary>Information-criterion comparison of candidate models.</summary>
    ModelSelection,
}

/// <summary>The termination status of a probability or statistical operation.</summary>
public enum DistributionStatus {
    /// <summary>The operation produced a mathematically usable result.</summary>
    Succeeded,

    /// <summary>An argument lies outside the operation's domain.</summary>
    InvalidInput,

    /// <summary>An observation, query, or parameter is not finite.</summary>
    NonFiniteInput,

    /// <summary>Too few observations were supplied for the requested estimate.</summary>
    InsufficientObservations,

    /// <summary>The sample has zero scale or otherwise makes the estimate undefined.</summary>
    DegenerateSample,

    /// <summary>A required matrix is exactly singular in the working precision.</summary>
    Singular,

    /// <summary>A matrix expected to be positive definite is not.</summary>
    NotPositiveDefinite,

    /// <summary>The requested moment does not exist for the distribution parameters.</summary>
    UndefinedStatistic,

    /// <summary>No registered candidate model can fit the sample.</summary>
    NoCandidateModel,

    /// <summary>A finite input produced a non-finite intermediate or result.</summary>
    NonFiniteOutput,

    /// <summary>The operation failed numerically for a reason not covered by another status.</summary>
    NumericalFailure,
}

/// <summary>A compact diagnostic report for probability and fitting operations.</summary>
/// <remarks>
/// Probability densities may legitimately be infinite at an isolated point, so
/// an infinite PDF value is accepted. CDF values and interior quantiles must be
/// finite; quantiles at probabilities zero and one may be infinite when a
/// distribution has unbounded support.
/// </remarks>
public readonly record struct DistributionOperationReport {
    internal DistributionOperationReport(
        DistributionOperation operation,
        DistributionStatus status,
        int observationCount = 0,
        int rejectedModelCount = 0) {
        Operation = operation;
        Status = status;
        ObservationCount = observationCount;
        RejectedModelCount = rejectedModelCount;
    }

    /// <summary>Gets the executed operation.</summary>
    public DistributionOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public DistributionStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status == DistributionStatus.Succeeded;

    /// <summary>Gets the number of observations used by a fitting operation.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets the number of candidate models rejected by model selection.</summary>
    public int RejectedModelCount { get; }
}

/// <summary>
/// Generic, allocation-free adapters for evaluating one-dimensional
/// probability distributions with explicit failure diagnostics.
/// </summary>
public static class DistributionEvaluation {
    /// <summary>
    /// Attempts to evaluate a probability density or mass function.
    /// </summary>
    /// <remarks>
    /// A positive infinity is accepted because a density can be singular at a
    /// point. NaN and negative density values are rejected; the former is
    /// reported as non-finite output and the latter as a numerical failure.
    /// </remarks>
    public static bool TryProbabilityFunction<TDistribution>(
        TDistribution distribution,
        double value,
        out double result,
        out DistributionOperationReport report)
        where TDistribution : IProbabilityDistribution {
        result = double.NaN;
        if (distribution is null || double.IsNaN(value)) {
            report = Report(
                DistributionOperation.ProbabilityFunction,
                DistributionStatus.InvalidInput);
            return false;
        }

        try {
            result = distribution.Pdf(value);
        } catch (ArithmeticException) {
            result = double.NaN;
            report = Report(DistributionOperation.ProbabilityFunction, DistributionStatus.NumericalFailure);
            return false;
        }

        if (double.IsNaN(result) || result < 0) {
            var status = double.IsNaN(result)
                ? DistributionStatus.NonFiniteOutput
                : DistributionStatus.NumericalFailure;
            result = double.NaN;
            report = Report(
                DistributionOperation.ProbabilityFunction,
                status);
            return false;
        }

        report = Report(DistributionOperation.ProbabilityFunction, DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to evaluate a cumulative distribution function.
    /// </summary>
    /// <remarks>
    /// A non-finite result is reported as
    /// <see cref="DistributionStatus.NonFiniteOutput"/>. A finite result outside
    /// the closed interval [0, 1] is reported as
    /// <see cref="DistributionStatus.NumericalFailure"/>.
    /// </remarks>
    public static bool TryCumulativeDistribution<TDistribution>(
        TDistribution distribution,
        double value,
        out double result,
        out DistributionOperationReport report)
        where TDistribution : IProbabilityDistribution {
        result = double.NaN;
        if (distribution is null || double.IsNaN(value)) {
            report = Report(
                DistributionOperation.CumulativeDistribution,
                DistributionStatus.InvalidInput);
            return false;
        }

        try {
            result = distribution.Cdf(value);
        } catch (ArithmeticException) {
            result = double.NaN;
            report = Report(DistributionOperation.CumulativeDistribution, DistributionStatus.NumericalFailure);
            return false;
        }

        if (!double.IsFinite(result) || result < 0 || result > 1) {
            var status = double.IsFinite(result)
                ? DistributionStatus.NumericalFailure
                : DistributionStatus.NonFiniteOutput;
            result = double.NaN;
            report = Report(
                DistributionOperation.CumulativeDistribution,
                status);
            return false;
        }

        report = Report(DistributionOperation.CumulativeDistribution, DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to evaluate a quantile function.
    /// </summary>
    /// <remarks>
    /// Probabilities zero and one are valid. Their infinite quantiles are
    /// accepted for unbounded support; every probability strictly between zero
    /// and one must produce a finite value.
    /// </remarks>
    public static bool TryQuantile<TDistribution>(
        TDistribution distribution,
        double probability,
        out double result,
        out DistributionOperationReport report)
        where TDistribution : IProbabilityDistribution {
        result = double.NaN;
        if (distribution is null || !double.IsFinite(probability) || probability < 0 || probability > 1) {
            report = Report(DistributionOperation.Quantile, DistributionStatus.InvalidInput);
            return false;
        }

        try {
            result = distribution.Quantile(probability);
        } catch (ArithmeticException) {
            result = double.NaN;
            report = Report(DistributionOperation.Quantile, DistributionStatus.NumericalFailure);
            return false;
        }

        var endpoint = probability == 0 || probability == 1;
        if (double.IsNaN(result) || (!endpoint && !double.IsFinite(result))) {
            result = double.NaN;
            report = Report(
                DistributionOperation.Quantile,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        report = Report(DistributionOperation.Quantile, DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts a normal maximum-likelihood fit and distinguishes an
    /// insufficient sample, invalid observations, and a zero-variance sample.
    /// </summary>
    public static bool TryNormalMaximumLikelihood(
        ReadOnlySpan<double> observations,
        out NormalFitResult result,
        out DistributionOperationReport report) {
        result = default;
        if (observations.Length < 2) {
            report = Report(
                DistributionOperation.NormalMaximumLikelihood,
                DistributionStatus.InsufficientObservations,
                observations.Length);
            return false;
        }

        for (var index = 0; index < observations.Length; index++) {
            if (!double.IsFinite(observations[index])) {
                report = Report(
                    DistributionOperation.NormalMaximumLikelihood,
                    DistributionStatus.NonFiniteInput,
                    observations.Length);
                return false;
            }
        }

        var mean = 0.0;
        var sumSquaredDeviations = 0.0;
        for (var index = 0; index < observations.Length; index++) {
            var count = index + 1.0;
            var difference = observations[index] - mean;
            mean += difference / count;
            sumSquaredDeviations += difference * (observations[index] - mean);
        }

        if (!double.IsFinite(mean) || !double.IsFinite(sumSquaredDeviations)) {
            report = Report(
                DistributionOperation.NormalMaximumLikelihood,
                DistributionStatus.NonFiniteOutput,
                observations.Length);
            return false;
        }

        var variance = Math.Max(0, sumSquaredDeviations) / observations.Length;
        var standardDeviation = Math.Sqrt(variance);
        if (!(standardDeviation > 0) || !double.IsFinite(standardDeviation)) {
            report = Report(
                DistributionOperation.NormalMaximumLikelihood,
                double.IsFinite(standardDeviation)
                    ? DistributionStatus.DegenerateSample
                    : DistributionStatus.NonFiniteOutput,
                observations.Length);
            return false;
        }

        var countAsDouble = observations.Length;
        result = new NormalFitResult(
            mean,
            standardDeviation,
            standardDeviation / Math.Sqrt(countAsDouble),
            standardDeviation / Math.Sqrt(2 * countAsDouble),
            observations.Length);
        report = Report(
            DistributionOperation.NormalMaximumLikelihood,
            DistributionStatus.Succeeded,
            observations.Length);
        return true;
    }

    /// <summary>
    /// Attempts an exponential maximum-likelihood fit and distinguishes an
    /// empty sample, invalid observations, and a zero sample mean.
    /// </summary>
    public static bool TryExponentialMaximumLikelihood(
        ReadOnlySpan<double> observations,
        out ExponentialFitResult result,
        out DistributionOperationReport report) {
        result = default;
        if (observations.IsEmpty) {
            report = Report(
                DistributionOperation.ExponentialMaximumLikelihood,
                DistributionStatus.InsufficientObservations);
            return false;
        }

        for (var index = 0; index < observations.Length; index++) {
            var value = observations[index];
            if (!double.IsFinite(value)) {
                report = Report(
                    DistributionOperation.ExponentialMaximumLikelihood,
                    DistributionStatus.NonFiniteInput,
                    observations.Length);
                return false;
            }

            if (value < 0) {
                report = Report(
                    DistributionOperation.ExponentialMaximumLikelihood,
                    DistributionStatus.InvalidInput,
                    observations.Length);
                return false;
            }
        }

        var sum = 0.0;
        for (var index = 0; index < observations.Length; index++) {
            sum += observations[index];
        }

        if (!double.IsFinite(sum)) {
            report = Report(
                DistributionOperation.ExponentialMaximumLikelihood,
                DistributionStatus.NonFiniteOutput,
                observations.Length);
            return false;
        }

        var mean = sum / observations.Length;
        if (!(mean > 0)) {
            report = Report(
                DistributionOperation.ExponentialMaximumLikelihood,
                DistributionStatus.DegenerateSample,
                observations.Length);
            return false;
        }

        var rate = 1 / mean;
        var rootCount = Math.Sqrt(observations.Length);
        result = new ExponentialFitResult(
            rate,
            mean,
            rate / rootCount,
            mean / rootCount,
            observations.Length);
        if (!double.IsFinite(rate) ||
            !double.IsFinite(result.RateStandardError) ||
            !double.IsFinite(result.MeanStandardError)) {
            result = default;
            report = Report(
                DistributionOperation.ExponentialMaximumLikelihood,
                DistributionStatus.NonFiniteOutput,
                observations.Length);
            return false;
        }

        report = Report(
            DistributionOperation.ExponentialMaximumLikelihood,
            DistributionStatus.Succeeded,
            observations.Length);
        return true;
    }

    private static DistributionOperationReport Report(
        DistributionOperation operation,
        DistributionStatus status,
        int observationCount = 0) {
        return new DistributionOperationReport(
            operation,
            status,
            observationCount);
    }

}
