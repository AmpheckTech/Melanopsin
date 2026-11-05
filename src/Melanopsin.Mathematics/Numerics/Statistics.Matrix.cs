// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Computes a population covariance matrix from row-major observations.
    /// </summary>
    /// <param name="observations">
    /// Row-major data with one observation per row and one variable per column.
    /// </param>
    /// <param name="observationCount">The number of rows to read.</param>
    /// <param name="variableCount">The number of columns to read.</param>
    public static double[] CovarianceMatrixPopulation(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount) {
        var destination = new double[checked(variableCount * variableCount)];
        var means = new double[variableCount];
        CovarianceMatrixPopulation(
            observations,
            observationCount,
            variableCount,
            destination,
            means);
        return destination;
    }

    /// <summary>
    /// Computes a population covariance matrix into caller-owned buffers.
    /// </summary>
    /// <remarks>
    /// The calculation uses a multivariate Welford recurrence: one sequential
    /// pass over the observations and <paramref name="variableCount"/> values
    /// of working storage. The result is symmetric and row-major.
    /// </remarks>
    public static void CovarianceMatrixPopulation(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means) {
        ComputeCovarianceMatrix(
            observations,
            observationCount,
            variableCount,
            destination,
            means,
            sample: false);
    }

    /// <summary>
    /// Computes an unbiased sample covariance matrix from row-major
    /// observations.
    /// </summary>
    public static double[] CovarianceMatrixSample(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount) {
        var destination = new double[checked(variableCount * variableCount)];
        var means = new double[variableCount];
        CovarianceMatrixSample(
            observations,
            observationCount,
            variableCount,
            destination,
            means);
        return destination;
    }

    /// <summary>
    /// Computes an unbiased sample covariance matrix into caller-owned buffers.
    /// </summary>
    public static void CovarianceMatrixSample(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means) {
        ComputeCovarianceMatrix(
            observations,
            observationCount,
            variableCount,
            destination,
            means,
            sample: true);
    }

    /// <summary>
    /// Computes a Pearson correlation matrix from row-major observations.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when a variable has exactly zero variance, in
    /// which case its correlation is mathematically undefined. The returned
    /// destination is then cleared.
    /// </returns>
    public static bool TryCorrelationMatrix(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means,
        Span<double> covarianceWorkspace) {
        return TryCorrelationMatrix(
            observations,
            observationCount,
            variableCount,
            destination,
            means,
            covarianceWorkspace,
            out _);
    }

    /// <summary>
    /// Computes a Pearson correlation matrix and returns a termination report.
    /// </summary>
    /// <remarks>
    /// Degeneracy is detected by an exactly zero diagonal variance in the
    /// working precision. No absolute near-zero variance threshold is applied.
    /// On failure the first <paramref name="variableCount"/> squared
    /// destination values are cleared. Non-finite observations are a numerical
    /// failure reported by this method; malformed dimensions and overlapping
    /// buffers remain programming errors and throw.
    /// </remarks>
    public static bool TryCorrelationMatrix(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means,
        Span<double> covarianceWorkspace,
        out CorrelationMatrixReport report) {
        var elementCount = checked(variableCount * variableCount);
        ValidateCovarianceMatrixArguments(
            observations,
            observationCount,
            variableCount,
            covarianceWorkspace,
            means);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (destination.Overlaps(covarianceWorkspace) ||
            destination.Overlaps(observations)) {
            throw new ArgumentException(
                "The destination must not overlap an input or workspace buffer.",
                nameof(destination));
        }

        var inputCount = checked(observationCount * variableCount);
        for (var index = 0; index < inputCount; index++) {
            if (!double.IsFinite(observations[index])) {
                destination[..elementCount].Clear();
                report = new CorrelationMatrixReport(
                    CorrelationMatrixStatus.NonFiniteInput,
                    observationCount,
                    variableCount);
                return false;
            }
        }

        ComputeCovarianceMatrix(
            observations,
            observationCount,
            variableCount,
            covarianceWorkspace,
            means,
            sample: false);
        for (var index = 0; index < variableCount; index++) {
            var variance = covarianceWorkspace[(index * variableCount) + index];
            if (variance == 0) {
                destination[..elementCount].Clear();
                report = new CorrelationMatrixReport(
                    CorrelationMatrixStatus.DegenerateSample,
                    observationCount,
                    variableCount);
                return false;
            }

            if (variance < 0 || !double.IsFinite(variance)) {
                destination[..elementCount].Clear();
                report = new CorrelationMatrixReport(
                    CorrelationMatrixStatus.NonFiniteOutput,
                    observationCount,
                    variableCount);
                return false;
            }
        }

        for (var row = 0; row < variableCount; row++) {
            var rowVariance = covarianceWorkspace[(row * variableCount) + row];
            for (var column = 0; column < variableCount; column++) {
                var columnVariance =
                    covarianceWorkspace[(column * variableCount) + column];
                var correlation = covarianceWorkspace[(row * variableCount) + column] /
                    Math.Sqrt(rowVariance * columnVariance);
                if (!double.IsFinite(correlation)) {
                    destination[..elementCount].Clear();
                    report = new CorrelationMatrixReport(
                        CorrelationMatrixStatus.NonFiniteOutput,
                        observationCount,
                        variableCount);
                    return false;
                }

                destination[(row * variableCount) + column] = correlation;
            }
        }

        report = new CorrelationMatrixReport(
            CorrelationMatrixStatus.Succeeded,
            observationCount,
            variableCount);
        return true;
    }

    /// <summary>
    /// Computes a Pearson correlation matrix from row-major observations.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A variable has exactly zero variance.
    /// </exception>
    public static double[] CorrelationMatrix(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount) {
        var destination = new double[checked(variableCount * variableCount)];
        var means = new double[variableCount];
        var covariance = new double[destination.Length];
        if (!TryCorrelationMatrix(
            observations,
            observationCount,
            variableCount,
            destination,
            means,
            covariance)) {
            throw new ArgumentException(
                "Correlation is undefined for a variable with zero variance.",
                nameof(observations));
        }

        return destination;
    }

    private static void ComputeCovarianceMatrix(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means,
        bool sample) {
        if (sample && observationCount < 2) {
            throw new ArgumentException(
                "At least two observations are required for a sample covariance matrix.",
                nameof(observationCount));
        }

        ValidateCovarianceMatrixArguments(
            observations,
            observationCount,
            variableCount,
            destination,
            means);
        var inputCount = checked(observationCount * variableCount);
        var outputCount = checked(variableCount * variableCount);
        for (var index = 0; index < inputCount; index++) {
            if (!double.IsFinite(observations[index])) {
                throw new ArgumentException(
                    "The observations must contain only finite values.",
                    nameof(observations));
            }
        }

        var covariance = destination[..outputCount];
        covariance.Clear();
        observations[..variableCount].CopyTo(means);
        for (var observation = 1; observation < observationCount; observation++) {
            var rowOffset = observation * variableCount;
            var count = observation + 1;
            for (var row = 0; row < variableCount; row++) {
                var value = observations[rowOffset + row];
                var delta = value - means[row];
                means[row] += delta / count;
                for (var column = 0; column <= row; column++) {
                    var update = delta * (observations[rowOffset + column] - means[column]);
                    covariance[(row * variableCount) + column] += update;
                }
            }
        }

        var divisor = sample ? observationCount - 1 : observationCount;
        for (var row = 0; row < variableCount; row++) {
            for (var column = 0; column <= row; column++) {
                var value = covariance[(row * variableCount) + column] / divisor;
                covariance[(row * variableCount) + column] = value;
                covariance[(column * variableCount) + row] = value;
            }
        }
    }
    private static void ValidateCovarianceMatrixArguments(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        Span<double> destination,
        Span<double> means) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observationCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variableCount);
        var inputCount = checked(observationCount * variableCount);
        var outputCount = checked(variableCount * variableCount);
        if (observations.Length < inputCount) {
            throw new ArgumentException(
                "The observations do not contain enough values.",
                nameof(observations));
        }

        if (destination.Length < outputCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (means.Length < variableCount) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(means));
        }

        if (destination.Overlaps(observations) || destination.Overlaps(means)) {
            throw new ArgumentException(
                "The destination must not overlap an input or workspace buffer.",
                nameof(destination));
        }
    }

}
