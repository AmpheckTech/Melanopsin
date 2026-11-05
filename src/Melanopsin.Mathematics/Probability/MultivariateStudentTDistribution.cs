namespace Melanopsin.Mathematics;

/// <summary>
/// A multivariate Student t distribution with a location vector, a symmetric
/// positive-definite scale matrix, and a positive degrees-of-freedom value.
/// </summary>
/// <remarks>
/// The scale matrix is not the covariance matrix. For more than two degrees of
/// freedom the covariance is <c>nu / (nu - 2)</c> times the scale matrix.
/// Symmetry is checked against an explicit relative tolerance; the default
/// requires exact symmetry. Calculations use a Cholesky factor rather than a
/// matrix inverse.
/// </remarks>
public sealed class MultivariateStudentTDistribution {
    private readonly double[] _location;
    private readonly double[] _scaleMatrix;
    private readonly double[] _lower;
    private readonly double _degreesOfFreedom;
    private readonly double _logNormalization;

    public MultivariateStudentTDistribution(
        ReadOnlySpan<double> location,
        ReadOnlySpan<double> scaleMatrix,
        double degreesOfFreedom,
        double relativeSymmetryTolerance = 0) {
        DistributionValidation.Scale(
            degreesOfFreedom,
            nameof(degreesOfFreedom));
        if (location.IsEmpty) {
            throw new ArgumentException(
                "The location vector must not be empty.",
                nameof(location));
        }

        if (!double.IsFinite(relativeSymmetryTolerance) ||
            relativeSymmetryTolerance < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeSymmetryTolerance));
        }

        var dimension = location.Length;
        var elementCount = checked(dimension * dimension);
        if (scaleMatrix.Length < elementCount) {
            throw new ArgumentException(
                "The scale matrix is too small.",
                nameof(scaleMatrix));
        }

        _location = location[..dimension].ToArray();
        _scaleMatrix = scaleMatrix[..elementCount].ToArray();
        var scale = 0.0;
        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(_location[index % dimension]) ||
                !double.IsFinite(_scaleMatrix[index])) {
                throw new ArgumentException(
                    "The location and scale matrix must contain only finite values.",
                    nameof(scaleMatrix));
            }

            scale = Math.Max(scale, Math.Abs(_scaleMatrix[index]));
        }


        for (var row = 0; row < dimension; row++) {
            for (var column = row + 1; column < dimension; column++) {
                var asymmetry = Math.Abs(
                    _scaleMatrix[(row * dimension) + column] -
                    _scaleMatrix[(column * dimension) + row]);
                if (asymmetry > relativeSymmetryTolerance * scale) {
                    throw new ArgumentException(
                        "The scale matrix must be symmetric within the requested tolerance.",
                        nameof(scaleMatrix));
                }
            }
        }

        var cholesky = new CholeskyDecomposition(_scaleMatrix, dimension);
        if (!cholesky.IsPositiveDefinite) {
            throw new ArgumentException(
                "The scale matrix must be positive definite.",
                nameof(scaleMatrix));
        }

        _lower = new double[elementCount];
        cholesky.CopyLowerTo(_lower);
        var logDeterminant = 0.0;
        for (var index = 0; index < dimension; index++) {
            logDeterminant += 2 * Math.Log(_lower[(index * dimension) + index]);
        }

        Dimension = dimension;
        _degreesOfFreedom = degreesOfFreedom;
        _logNormalization =
            SpecialFunctions.LogGamma(0.5 * (degreesOfFreedom + dimension)) -
            SpecialFunctions.LogGamma(0.5 * degreesOfFreedom) -
            (0.5 * dimension * Math.Log(degreesOfFreedom * Constants.Pi)) -
            (0.5 * logDeterminant);
    }

    public int Dimension { get; }

    public double DegreesOfFreedom => _degreesOfFreedom;

    public ReadOnlySpan<double> Location => _location;

    public ReadOnlySpan<double> ScaleMatrix => _scaleMatrix;

    public bool HasFiniteMean => _degreesOfFreedom > 1;

    public bool HasFiniteCovariance => _degreesOfFreedom > 2;

    /// <summary>
    /// Returns the covariance matrix when it exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The degrees of freedom are at most two.
    /// </exception>
    public double[] CovarianceMatrix() {
        if (!HasFiniteCovariance) {
            throw new InvalidOperationException(
                "The covariance is defined only above two degrees of freedom.");
        }

        var factor = _degreesOfFreedom / (_degreesOfFreedom - 2);
        var result = new double[checked(Dimension * Dimension)];
        for (var index = 0; index < result.Length; index++) {
            result[index] = factor * _scaleMatrix[index];
        }

        return result;
    }

    /// <summary>Computes the log density with allocated workspace.</summary>
    public double LogPdf(ReadOnlySpan<double> value) {
        return TryLogPdf(value, new double[Dimension], out var result)
            ? result
            : double.NaN;
    }

    /// <summary>Computes the log density without allocating.</summary>
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
            var difference = value[row] - _location[row];
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

        var logarithmArgument = 1 + (quadraticForm / _degreesOfFreedom);
        if (!double.IsFinite(logarithmArgument)) {
            result = double.NaN;
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        if (logarithmArgument <= 0) {
            result = double.NaN;
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.NumericalFailure);
            return false;
        }

        result = _logNormalization -
            (0.5 * (_degreesOfFreedom + Dimension) *
                Math.Log(logarithmArgument));
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

    /// <summary>Draws a vector without allocating.</summary>
    /// <returns><see langword="true"/> when every sampled component is finite.</returns>
    public bool TrySample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator) {
        return TrySample(destination, workspace, ref generator, out _);
    }

    /// <summary>
    /// Draws a vector and returns a diagnostic report.
    /// </summary>
    /// <param name="destination">Receives <see cref="Dimension"/> finite values.</param>
    /// <param name="workspace">The caller-owned calculation workspace.</param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when every sampled component is finite.</returns>
    /// <remarks>
    /// The destination is cleared if a finite sample cannot be represented.
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
            var sum = 0.0;
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

        var chiSquare = 2 * DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * _degreesOfFreedom);
        if (!double.IsFinite(chiSquare) || !(chiSquare > 0)) {
            destination[..Dimension].Clear();
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        var radialScale = Math.Sqrt(_degreesOfFreedom / chiSquare);
        if (!double.IsFinite(radialScale)) {
            destination[..Dimension].Clear();
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        for (var index = 0; index < Dimension; index++) {
            var sample = _location[index] + (radialScale * workspace[index]);
            if (!double.IsFinite(sample)) {
                destination[..Dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.Sampling,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }

            destination[index] = sample;
        }

        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>Draws a vector using caller-owned storage.</summary>
    /// <exception cref="ArithmeticException">
    /// A finite sample could not be represented.
    /// </exception>
    public void Sample(
        Span<double> destination,
        Span<double> workspace,
        ref Xoshiro256StarStar generator) {
        if (!TrySample(destination, workspace, ref generator)) {
            throw new ArithmeticException(
                "The multivariate Student-t sample is not finite.");
        }
    }

    public double[] Sample(ref Xoshiro256StarStar generator) {
        var result = new double[Dimension];
        Sample(result, new double[Dimension], ref generator);
        return result;
    }
}
