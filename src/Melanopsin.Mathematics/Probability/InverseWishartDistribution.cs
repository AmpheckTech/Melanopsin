// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An inverse-Wishart distribution over real symmetric positive-definite
/// matrices.
/// </summary>
/// <remarks>
/// The scale matrix uses row-major storage and must be symmetric
/// positive-definite. Log densities are evaluated by solving with a Cholesky
/// factor of the observation rather than explicitly forming its inverse. This
/// is the inverse-Wishart distribution, the law of the inverse of a Wishart
/// matrix (Wishart, 1928,
/// <see href="https://doi.org/10.1093/biomet/20A.1-2.32">doi:10.1093/biomet/20A.1-2.32</see>).
/// </remarks>
public sealed class InverseWishartDistribution {
    private readonly double[] _scale;
    private readonly double[] _lowerInverseScale;
    private readonly double _logNormalization;
    private readonly double _symmetryTolerance;

    public InverseWishartDistribution(
        ReadOnlySpan<double> scale,
        int dimension,
        double degreesOfFreedom,
        double relativeSymmetryTolerance = 0) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimension);
        if (!double.IsFinite(degreesOfFreedom) ||
            degreesOfFreedom <= dimension - 1) {
            throw new ArgumentOutOfRangeException(nameof(degreesOfFreedom));
        }

        if (!double.IsFinite(relativeSymmetryTolerance) ||
            relativeSymmetryTolerance < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeSymmetryTolerance));
        }

        var elementCount = checked(dimension * dimension);
        if (scale.Length < elementCount) {
            throw new ArgumentException(
                "The scale matrix is too small.",
                nameof(scale));
        }

        _scale = scale[..elementCount].ToArray();
        var lowerScale = new double[elementCount];
        if (!WishartDistributionMath.TryFactor(
            _scale,
            dimension,
            relativeSymmetryTolerance,
            lowerScale,
            out var logDeterminant)) {
            throw new ArgumentException(
                "The scale matrix must be finite, symmetric, and positive definite.",
                nameof(scale));
        }

        var inverseScale = new double[elementCount];
        var inverseWorkspace = new double[dimension];
        if (!WishartDistributionMath.TryInvertSpd(
            lowerScale,
            inverseScale,
            inverseWorkspace,
            dimension)) {
            throw new ArgumentException(
                "The scale matrix must have a finite inverse.",
                nameof(scale));
        }

        _lowerInverseScale = new double[elementCount];
        if (!WishartDistributionMath.TryFactor(
            inverseScale,
            dimension,
            relativeSymmetryTolerance,
            _lowerInverseScale,
            out _)) {
            throw new ArgumentException(
                "The inverse scale matrix must be positive definite.",
                nameof(scale));
        }

        Dimension = dimension;
        DegreesOfFreedom = degreesOfFreedom;
        _symmetryTolerance = relativeSymmetryTolerance;
        _logNormalization =
            (0.5 * degreesOfFreedom * logDeterminant) -
            (0.5 * degreesOfFreedom * dimension * Constants.Ln2) -
            SpecialFunctions.LogMultivariateGamma(
                0.5 * degreesOfFreedom,
                dimension);
    }

    public int Dimension { get; }

    public double DegreesOfFreedom { get; }

    public ReadOnlySpan<double> Scale => _scale;

    /// <summary>
    /// Gets whether the inverse-Wishart mean matrix is finite.
    /// </summary>
    public bool HasFiniteMean =>
        DegreesOfFreedom > Dimension + 1;

    /// <summary>
    /// Computes the log density using an allocated temporary workspace.
    /// </summary>
    public double LogPdf(ReadOnlySpan<double> value) {
        var elementCount = checked(Dimension * Dimension);
        var workspace = new double[elementCount + (2 * Dimension)];
        return TryLogPdf(value, workspace, out var result)
            ? result
            : double.NaN;
    }

    /// <summary>
    /// Computes the log density without allocating.
    /// </summary>
    /// <param name="value">At least <c>Dimension * Dimension</c> row-major values.</param>
    /// <param name="workspace">
    /// At least <c>Dimension * Dimension + 2 * Dimension</c> values.
    /// </param>
    /// <param name="result">
    /// Receives the log density, including negative infinity outside the
    /// positive-definite support.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the input is finite and the calculation
    /// completed; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryLogPdf(
        ReadOnlySpan<double> value,
        Span<double> workspace,
        out double result) {
        return TryLogPdf(value, workspace, out result, out _);
    }

    /// <summary>
    /// Computes the log density and returns a diagnostic report.
    /// </summary>
    /// <param name="value">At least <c>Dimension * Dimension</c> row-major values.</param>
    /// <param name="workspace">The caller-owned calculation workspace.</param>
    /// <param name="result">
    /// Receives the log density, including negative infinity outside the
    /// positive-definite support.
    /// </param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> for a usable (possibly infinite) log density.</returns>
    public bool TryLogPdf(
        ReadOnlySpan<double> value,
        Span<double> workspace,
        out double result,
        out DistributionOperationReport report) {
        var dimension = Dimension;
        var elementCount = checked(dimension * dimension);
        if (value.Length < elementCount) {
            throw new ArgumentException(
                "The observation matrix is too small.",
                nameof(value));
        }

        if (workspace.Length < elementCount + (2 * dimension)) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (value[..elementCount].Overlaps(workspace)) {
            throw new ArgumentException(
                "The observation and workspace must not overlap.",
                nameof(workspace));
        }

        if (!IsFinite(value, elementCount)) {
            result = double.NaN;
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.NonFiniteInput);
            return false;
        }

        var lowerValue = workspace[..elementCount];
        if (!WishartDistributionMath.TryFactor(
            value,
            dimension,
            _symmetryTolerance,
            lowerValue,
            out var logDeterminant)) {
            result = double.NegativeInfinity;
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.Succeeded);
            return true;
        }

        var rightHandSide = workspace.Slice(elementCount, dimension);
        var solution = workspace.Slice(elementCount + dimension, dimension);
        var trace = 0.0;
        for (var column = 0; column < dimension; column++) {
            for (var row = 0; row < dimension; row++) {
                rightHandSide[row] = _scale[(row * dimension) + column];
            }

            if (!WishartDistributionMath.TrySolveCholesky(
                lowerValue,
                rightHandSide,
                solution,
                dimension,
                out var solveReport)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    solveReport.Status);
                return false;
            }

            trace += solution[column];
        }

        result = (-0.5 * (DegreesOfFreedom + dimension + 1) * logDeterminant) -
            (0.5 * trace) +
            _logNormalization;
        if (double.IsNaN(result)) {
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        report = new DistributionOperationReport(
            DistributionOperation.LogProbabilityFunction,
            DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Writes the mean matrix <c>scale / (degreesOfFreedom - Dimension - 1)</c>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> and clears the destination when the mean does
    /// not exist.
    /// </returns>
    public bool TryCopyMeanTo(Span<double> destination) {
        return TryCopyMeanTo(destination, out _);
    }

    /// <summary>
    /// Writes the mean matrix and returns a diagnostic report.
    /// </summary>
    /// <param name="destination">Receives the row-major mean matrix.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when the mean exists and is finite.</returns>
    public bool TryCopyMeanTo(
        Span<double> destination,
        out DistributionOperationReport report) {
        var elementCount = checked(_scale.Length);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        if (!HasFiniteMean) {
            destination[..elementCount].Clear();
            report = new DistributionOperationReport(
                DistributionOperation.Mean,
                DistributionStatus.UndefinedStatistic);
            return false;
        }

        var denominator = DegreesOfFreedom - Dimension - 1;
        for (var index = 0; index < elementCount; index++) {
            destination[index] = _scale[index] / denominator;
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(destination[index])) {
                destination[..elementCount].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.Mean,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new DistributionOperationReport(
            DistributionOperation.Mean,
            DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Draws a matrix by inverting a Bartlett Wishart sample.
    /// </summary>
    public double[] Sample(ref Xoshiro256StarStar generator) {
        var elementCount = checked(Dimension * Dimension);
        var result = new double[elementCount];
        var workspace = new double[(2 * elementCount) + Dimension];
        if (!TrySample(result, workspace, ref generator)) {
            throw new ArithmeticException(
                "The inverse-Wishart sample could not be represented as finite values.");
        }

        return result;
    }

    /// <summary>
    /// Draws a matrix by inverting a Bartlett Wishart sample without allocating.
    /// </summary>
    /// <param name="destination">
    /// Receives <c>Dimension * Dimension</c> row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least <c>2 * Dimension * Dimension + Dimension</c> values.
    /// </param>
    /// <param name="generator">The pseudo-random generator state.</param>
    public bool TrySample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator) {
        return TrySample(
            destination,
            workspace,
            ref generator,
            out _);
    }

    /// <summary>
    /// Draws an inverse-Wishart matrix and returns a diagnostic report.
    /// </summary>
    /// <param name="destination">Receives the row-major matrix sample.</param>
    /// <param name="workspace">The caller-owned calculation workspace.</param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when every sampled entry is finite.</returns>
    public bool TrySample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator,
        out DistributionOperationReport report) {
        var dimension = Dimension;
        var elementCount = checked(dimension * dimension);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        var requiredWorkspace = checked((2 * elementCount) + dimension);
        if (workspace.Length < requiredWorkspace) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (destination[..elementCount].Overlaps(workspace)) {
            throw new ArgumentException(
                "The destination and workspace must not overlap.",
                nameof(workspace));
        }

        var first = workspace[..elementCount];
        var second = workspace.Slice(elementCount, elementCount);
        var temporary = workspace.Slice(2 * elementCount, dimension);
        for (var row = 0; row < dimension; row++) {
            var rowOffset = row * dimension;
            for (var column = 0; column <= row; column++) {
                if (row == column) {
                    var shape = 0.5 * (DegreesOfFreedom - column);
                    var gamma = DistributionValidation.SampleStandardGamma(
                        ref generator,
                        shape);
                    first[rowOffset + column] = Math.Sqrt(2 * gamma);
                } else {
                    first[rowOffset + column] =
                        DistributionValidation.SampleNormal(ref generator);
                }
            }
        }

        WishartDistributionMath.MultiplyLowerByLower(
            _lowerInverseScale,
            first,
            second,
            dimension);
        WishartDistributionMath.SquareLowerProduct(
            second,
            destination,
            dimension);
        if (!IsFinite(destination, elementCount)) {
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        if (!WishartDistributionMath.TryFactor(
            destination,
            dimension,
            _symmetryTolerance,
            first,
            out _,
            out var factorReport)) {
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                factorReport.Status);
            return false;
        }

        if (!WishartDistributionMath.TryInvertSpd(
            first,
            second,
            temporary,
            dimension,
            out var inversionReport)) {
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                inversionReport.Status);
            return false;
        }

        second[..elementCount].CopyTo(destination);
        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            DistributionStatus.Succeeded);
        return true;
    }

    private static bool IsFinite(ReadOnlySpan<double> values, int count) {
        for (var index = 0; index < count; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }
}
