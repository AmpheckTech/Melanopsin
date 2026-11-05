namespace Melanopsin.Mathematics;

/// <summary>
/// A multivariate normal distribution parameterized by a mean vector and a
/// symmetric positive-definite covariance matrix.
/// </summary>
/// <remarks>
/// The covariance matrix uses row-major storage. Symmetry is checked against an
/// explicit relative tolerance; the default requires exact symmetry.
/// Calculations use a Cholesky factor and never form the covariance inverse.
/// </remarks>
public sealed class MultivariateNormalDistribution {
    private readonly double[] _mean;
    private readonly double[] _covariance;
    private readonly double[] _lower;
    private readonly double _logNormalization;

    public MultivariateNormalDistribution(
        ReadOnlySpan<double> mean,
        ReadOnlySpan<double> covariance,
        double relativeSymmetryTolerance = 0) {
        if (mean.Length == 0) {
            throw new ArgumentException(
                "The mean vector must not be empty.",
                nameof(mean));
        }

        if (!double.IsFinite(relativeSymmetryTolerance) ||
            relativeSymmetryTolerance < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeSymmetryTolerance));
        }

        var dimension = mean.Length;
        var elementCount = checked(dimension * dimension);
        if (covariance.Length < elementCount) {
            throw new ArgumentException(
                "The covariance matrix is too small.",
                nameof(covariance));
        }

        _mean = mean[..dimension].ToArray();
        for (var index = 0; index < dimension; index++) {
            if (!double.IsFinite(_mean[index])) {
                throw new ArgumentException(
                    "The mean vector must contain only finite values.",
                    nameof(mean));
            }
        }

        _covariance = covariance[..elementCount].ToArray();
        var scale = 0.0;
        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(_covariance[index])) {
                throw new ArgumentException(
                    "The covariance matrix must contain only finite values.",
                    nameof(covariance));
            }

            scale = Math.Max(scale, Math.Abs(_covariance[index]));
        }


        for (var row = 0; row < dimension; row++) {
            for (var column = row + 1; column < dimension; column++) {
                var asymmetry = Math.Abs(
                    _covariance[(row * dimension) + column] -
                    _covariance[(column * dimension) + row]);
                if (asymmetry > relativeSymmetryTolerance * scale) {
                    throw new ArgumentException(
                        "The covariance matrix must be symmetric within the requested tolerance.",
                        nameof(covariance));
                }
            }
        }

        var cholesky = new CholeskyDecomposition(_covariance, dimension);
        if (!cholesky.IsPositiveDefinite) {
            throw new ArgumentException(
                "The covariance matrix must be positive definite.",
                nameof(covariance));
        }

        _lower = new double[elementCount];
        cholesky.CopyLowerTo(_lower);
        var logDeterminant = 0.0;
        for (var index = 0; index < dimension; index++) {
            logDeterminant += 2 * Math.Log(_lower[(index * dimension) + index]);
        }

        Dimension = dimension;
        _logNormalization = -0.5 *
            ((dimension * Constants.Ln2Pi) + logDeterminant);
    }

    public int Dimension { get; }

    public ReadOnlySpan<double> MeanVector => _mean;

    public ReadOnlySpan<double> Covariance => _covariance;

    /// <summary>
    /// Computes the log density using an allocated temporary workspace.
    /// </summary>
    public double LogPdf(ReadOnlySpan<double> value) {
        var workspace = new double[Dimension];
        return TryLogPdf(value, workspace, out var result)
            ? result
            : double.NaN;
    }

    /// <summary>
    /// Computes the log density without allocating.
    /// </summary>
    /// <param name="value">
    /// At least <see cref="Dimension"/> coordinates.
    /// </param>
    /// <param name="workspace">
    /// At least <see cref="Dimension"/> values; it must not overlap
    /// <paramref name="value"/>.
    /// </param>
    /// <param name="result">Receives the finite log density on success.</param>
    public bool TryLogPdf(
        ReadOnlySpan<double> value,
        Span<double> workspace,
        out double result) {
        return TryLogPdf(value, workspace, out result, out _);
    }

    /// <summary>
    /// Computes the log density and returns a diagnostic report.
    /// </summary>
    /// <param name="value">At least <see cref="Dimension"/> coordinates.</param>
    /// <param name="workspace">At least <see cref="Dimension"/> scratch values.</param>
    /// <param name="result">Receives the finite log density on success.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when a finite log density was produced.</returns>
    public bool TryLogPdf(
        ReadOnlySpan<double> value,
        Span<double> workspace,
        out double result,
        out DistributionOperationReport report) {
        if (value.Length < Dimension) {
            throw new ArgumentException(
                "The coordinate vector is too small.",
                nameof(value));
        }

        if (workspace.Length < Dimension) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (value.Overlaps(workspace)) {
            throw new ArgumentException(
                "The workspace must not overlap the coordinate vector.",
                nameof(workspace));
        }

        var quadraticForm = 0.0;
        for (var row = 0; row < Dimension; row++) {
            var difference = value[row] - _mean[row];
            if (!double.IsFinite(difference)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    DistributionStatus.NonFiniteInput);
                return false;
            }

            for (var column = 0; column < row; column++) {
                difference -= _lower[(row * Dimension) + column] *
                    workspace[column];
            }

            var transformed = difference /
                _lower[(row * Dimension) + row];
            workspace[row] = transformed;
            quadraticForm += transformed * transformed;
            if (!double.IsFinite(quadraticForm)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }
        }

        result = _logNormalization - (0.5 * quadraticForm);
        if (!double.IsFinite(result)) {
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

    public double Pdf(ReadOnlySpan<double> value) {
        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Draws a coordinate vector without allocating.
    /// </summary>
    /// <param name="destination">
    /// At least <see cref="Dimension"/> values.
    /// </param>
    /// <param name="workspace">
    /// At least <see cref="Dimension"/> values and no overlap with the
    /// destination.
    /// </param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <returns><see langword="true"/> when every sampled component is finite.</returns>
    public bool TrySample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator) {
        return TrySample(destination, workspace, ref generator, out _);
    }

    /// <summary>
    /// Draws a coordinate vector and returns a diagnostic report.
    /// </summary>
    /// <param name="destination">Receives <see cref="Dimension"/> finite values.</param>
    /// <param name="workspace">The caller-owned calculation workspace.</param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when every sampled component is finite.</returns>
    /// <remarks>
    /// The destination is cleared if a finite sample cannot be represented.
    /// Generation never repeats a rejected draw because the generator state is
    /// always advanced deterministically.
    /// </remarks>
    public bool TrySample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator,
        out DistributionOperationReport report) {
        if (destination.Length < Dimension) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        if (workspace.Length < Dimension) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (destination.Overlaps(workspace)) {
            throw new ArgumentException(
                "The destination and workspace must not overlap.",
                nameof(workspace));
        }

        for (var index = 0; index < Dimension; index++) {
            destination[index] = DistributionValidation.SampleNormal(
                ref generator);
        }

        for (var row = 0; row < Dimension; row++) {
            var sum = _mean[row];
            for (var column = 0; column <= row; column++) {
                sum += _lower[(row * Dimension) + column] *
                    destination[column];
            }

            if (!double.IsFinite(sum)) {
                destination[..Dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.Sampling,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }

            workspace[row] = sum;
        }

        workspace[..Dimension].CopyTo(destination);
        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Draws a coordinate vector using caller-owned storage.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// A finite sample could not be represented.
    /// </exception>
    public void Sample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator) {
        if (!TrySample(destination, workspace, ref generator)) {
            throw new ArithmeticException(
                "The multivariate-normal sample is not finite.");
        }
    }

    public double[] Sample(ref Xoshiro256StarStar generator) {
        var result = new double[Dimension];
        var workspace = new double[Dimension];
        Sample(result, workspace, ref generator);
        return result;
    }
}
