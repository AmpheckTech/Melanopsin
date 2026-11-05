namespace Melanopsin.Mathematics;

/// <summary>
/// A matrix normal distribution over real <see cref="RowCount"/>-by-
/// <see cref="ColumnCount"/> matrices.
/// </summary>
/// <remarks>
/// The mean and every matrix use row-major storage. If <c>X</c> has this
/// distribution, then <c>vec(X)</c> is multivariate normal with covariance
/// <c>columnCovariance ⊗ rowCovariance</c>, where <c>⊗</c> is the Kronecker
/// product. The row and column covariance matrices must be symmetric positive
/// definite. Symmetry is checked against an explicit relative tolerance; the
/// default requires exact symmetry. The matrix-variate notation and density
/// follow <see href="https://doi.org/10.1093/biomet/68.1.265">Dawid (1981)</see>.
/// </remarks>
public sealed class MatrixNormalDistribution {
    private readonly double[] _mean;
    private readonly double[] _rowCovariance;
    private readonly double[] _columnCovariance;
    private readonly double[] _lowerRow;
    private readonly double[] _lowerColumn;
    private readonly double _logNormalization;

    public MatrixNormalDistribution(
        ReadOnlySpan<double> mean,
        int rowCount,
        int columnCount,
        ReadOnlySpan<double> rowCovariance,
        ReadOnlySpan<double> columnCovariance,
        double relativeSymmetryTolerance = 0) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnCount);
        if (!double.IsFinite(relativeSymmetryTolerance) ||
            relativeSymmetryTolerance < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeSymmetryTolerance));
        }

        var matrixElementCount = checked(rowCount * columnCount);
        var rowElementCount = checked(rowCount * rowCount);
        var columnElementCount = checked(columnCount * columnCount);
        if (mean.Length < matrixElementCount) {
            throw new ArgumentException(
                "The mean matrix is too small.",
                nameof(mean));
        }

        if (rowCovariance.Length < rowElementCount) {
            throw new ArgumentException(
                "The row covariance matrix is too small.",
                nameof(rowCovariance));
        }

        if (columnCovariance.Length < columnElementCount) {
            throw new ArgumentException(
                "The column covariance matrix is too small.",
                nameof(columnCovariance));
        }

        _mean = mean[..matrixElementCount].ToArray();
        ValidateFinite(_mean, nameof(mean), "The mean matrix");
        _rowCovariance = rowCovariance[..rowElementCount].ToArray();
        _columnCovariance = columnCovariance[..columnElementCount].ToArray();
        _lowerRow = FactorSpd(
            _rowCovariance,
            rowCount,
            relativeSymmetryTolerance,
            nameof(rowCovariance),
            out var logDeterminantRow);
        _lowerColumn = FactorSpd(
            _columnCovariance,
            columnCount,
            relativeSymmetryTolerance,
            nameof(columnCovariance),
            out var logDeterminantColumn);

        RowCount = rowCount;
        ColumnCount = columnCount;
        _logNormalization = -0.5 *
            ((matrixElementCount * Constants.Ln2Pi) +
                (columnCount * logDeterminantRow) +
                (rowCount * logDeterminantColumn));
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public ReadOnlySpan<double> MeanMatrix => _mean;

    public ReadOnlySpan<double> RowCovariance => _rowCovariance;

    public ReadOnlySpan<double> ColumnCovariance => _columnCovariance;

    /// <summary>
    /// Computes the log density using an allocated temporary workspace.
    /// </summary>
    public double LogPdf(ReadOnlySpan<double> value) {
        var workspace = new double[
            checked((RowCount * ColumnCount) +
                (2 * Math.Max(RowCount, ColumnCount)))];
        return TryLogPdf(value, workspace, out var result)
            ? result
            : double.NaN;
    }

    /// <summary>
    /// Computes the log density without allocating.
    /// </summary>
    /// <param name="value">
    /// At least <c>RowCount * ColumnCount</c> row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least
    /// <c>RowCount * ColumnCount + 2 * max(RowCount, ColumnCount)</c> values.
    /// </param>
    /// <param name="result">
    /// Receives the log density when the calculation succeeds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the input and calculation are finite;
    /// otherwise <see langword="false"/> and <paramref name="result"/> is NaN.
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
    /// <param name="value">At least <c>RowCount * ColumnCount</c> row-major values.</param>
    /// <param name="workspace">The caller-owned calculation workspace.</param>
    /// <param name="result">Receives the finite log density on success.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when a finite log density was produced.</returns>
    public bool TryLogPdf(
        ReadOnlySpan<double> value,
        Span<double> workspace,
        out double result,
        out DistributionOperationReport report) {
        var rowCount = RowCount;
        var columnCount = ColumnCount;
        var matrixElementCount = checked(rowCount * columnCount);
        var maximumDimension = Math.Max(rowCount, columnCount);
        if (value.Length < matrixElementCount) {
            throw new ArgumentException(
                "The observation matrix is too small.",
                nameof(value));
        }

        if (workspace.Length <
            matrixElementCount + (2 * maximumDimension)) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (value[..matrixElementCount].Overlaps(workspace)) {
            throw new ArgumentException(
                "The observation and workspace must not overlap.",
                nameof(workspace));
        }

        if (!IsFinite(value, matrixElementCount)) {
            result = double.NaN;
            report = new DistributionOperationReport(
                DistributionOperation.LogProbabilityFunction,
                DistributionStatus.NonFiniteInput);
            return false;
        }

        var transformed = workspace[..matrixElementCount];
        var rightHandSide = workspace.Slice(
            matrixElementCount,
            maximumDimension);
        var solution = workspace.Slice(
            matrixElementCount + maximumDimension,
            maximumDimension);

        // Whiten columns: W = (X - M) L_V^{-T}. If V = L_V L_V^T,
        // this leaves W W^T = (X - M) V^{-1} (X - M)^T.
        for (var row = 0; row < rowCount; row++) {
            var source = value.Slice(row * columnCount, columnCount);
            var destination = transformed.Slice(row * columnCount, columnCount);
            for (var column = 0; column < columnCount; column++) {
                var difference = source[column] - _mean[(row * columnCount) + column];
                if (!double.IsFinite(difference)) {
                    result = double.NaN;
                    report = new DistributionOperationReport(
                        DistributionOperation.LogProbabilityFunction,
                        DistributionStatus.NonFiniteOutput);
                    return false;
                }

                rightHandSide[column] = difference;
            }

            if (!TriangularSolve.TrySolve(
                _lowerColumn,
                columnCount,
                MatrixTriangle.Lower,
                unitDiagonal: false,
                rightHandSide[..columnCount],
                destination[..columnCount],
                out var solveReport)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    MapSolveStatus(solveReport.Status));
                return false;
            }
        }

        // The remaining quadratic form is trace(W^T U^{-1} W).
        var quadraticForm = 0.0;
        for (var column = 0; column < columnCount; column++) {
            for (var row = 0; row < rowCount; row++) {
                rightHandSide[row] = transformed[(row * columnCount) + column];
            }

            if (!TriangularSolve.TrySolve(
                _lowerRow,
                rowCount,
                MatrixTriangle.Lower,
                unitDiagonal: false,
                rightHandSide[..rowCount],
                solution[..rowCount],
                out var solveReport)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    MapSolveStatus(solveReport.Status));
                return false;
            }

            for (var row = 0; row < rowCount; row++) {
                quadraticForm += solution[row] * solution[row];
            }

            if (!double.IsFinite(quadraticForm)) {
                result = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.LogProbabilityFunction,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }
        }

        result = _logNormalization - (0.5 * quadraticForm);
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

    public double Pdf(ReadOnlySpan<double> value) {
        return Math.Exp(LogPdf(value));
    }

    public void CopyMeanTo(Span<double> destination) {
        var elementCount = checked(_mean.Length);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        _mean.CopyTo(destination);
    }

    /// <summary>
    /// Draws a matrix without allocating.
    /// </summary>
    /// <param name="destination">
    /// Receives <c>RowCount * ColumnCount</c> row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least
    /// <c>RowCount * ColumnCount + ColumnCount</c> values and no overlap with
    /// the destination.
    /// </param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <returns><see langword="false"/> if a non-finite value is produced.</returns>
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
    /// Draws a matrix and returns a diagnostic report.
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
        var rowCount = RowCount;
        var columnCount = ColumnCount;
        var matrixElementCount = checked(rowCount * columnCount);
        if (destination.Length < matrixElementCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        if (workspace.Length < matrixElementCount + columnCount) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (destination[..matrixElementCount].Overlaps(workspace)) {
            throw new ArgumentException(
                "The destination and workspace must not overlap.",
                nameof(workspace));
        }

        var standardNormal = workspace[..matrixElementCount];
        var transformedRow = workspace.Slice(matrixElementCount, columnCount);
        for (var index = 0; index < matrixElementCount; index++) {
            standardNormal[index] = DistributionValidation.SampleNormal(
                ref generator);
        }

        for (var row = 0; row < rowCount; row++) {
            for (var column = 0; column < columnCount; column++) {
                var sum = 0.0;
                for (var inner = 0; inner <= row; inner++) {
                    sum += _lowerRow[(row * rowCount) + inner] *
                        standardNormal[(inner * columnCount) + column];
                }

                transformedRow[column] = sum;
            }

            for (var column = 0; column < columnCount; column++) {
                var sum = _mean[(row * columnCount) + column];
                for (var inner = 0; inner <= column; inner++) {
                    sum += transformedRow[inner] *
                        _lowerColumn[(column * columnCount) + inner];
                }

                destination[(row * columnCount) + column] = sum;
                if (!double.IsFinite(sum)) {
                    report = new DistributionOperationReport(
                        DistributionOperation.Sampling,
                        DistributionStatus.NonFiniteOutput);
                    return false;
                }
            }
        }

        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            DistributionStatus.Succeeded);
        return true;
    }

    /// <summary>Draws a matrix using allocated temporary storage.</summary>
    public double[] Sample(ref Xoshiro256StarStar generator) {
        var result = new double[checked(RowCount * ColumnCount)];
        var workspace = new double[result.Length + ColumnCount];
        if (!TrySample(result, workspace, ref generator)) {
            throw new ArithmeticException(
                "The matrix-normal sample could not be represented as finite values.");
        }

        return result;
    }

    private static double[] FactorSpd(
        ReadOnlySpan<double> matrix,
        int order,
        double relativeSymmetryTolerance,
        string parameterName,
        out double logDeterminant) {
        ValidateFinite(matrix, parameterName, "The covariance matrix");
        var scale = 0.0;
        for (var index = 0; index < matrix.Length; index++) {
            scale = Math.Max(scale, Math.Abs(matrix[index]));
        }

        for (var row = 0; row < order; row++) {
            for (var column = row + 1; column < order; column++) {
                var asymmetry = Math.Abs(
                    matrix[(row * order) + column] -
                    matrix[(column * order) + row]);
                if (asymmetry > relativeSymmetryTolerance * scale) {
                    throw new ArgumentException(
                        "The covariance matrix must be symmetric within the requested relative tolerance.",
                        parameterName);
                }
            }
        }

        var cholesky = new CholeskyDecomposition(matrix[..(order * order)], order);
        if (!cholesky.IsPositiveDefinite) {
            throw new ArgumentException(
                "The covariance matrix must be positive definite.",
                parameterName);
        }

        var lower = new double[checked(order * order)];
        cholesky.CopyLowerTo(lower);
        logDeterminant = 0.0;
        for (var index = 0; index < order; index++) {
            logDeterminant += 2 * Math.Log(lower[(index * order) + index]);
        }

        return lower;
    }

    private static void ValidateFinite(
        ReadOnlySpan<double> values,
        string parameterName,
        string description) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    $"{description} must contain only finite values.",
                    parameterName);
            }
        }
    }

    private static bool IsFinite(ReadOnlySpan<double> values, int count) {
        for (var index = 0; index < count; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static DistributionStatus MapSolveStatus(
        LinearAlgebraStatus status) {
        return status switch {
            LinearAlgebraStatus.Succeeded => DistributionStatus.Succeeded,
            LinearAlgebraStatus.NonFiniteInput => DistributionStatus.NonFiniteInput,
            LinearAlgebraStatus.DegenerateInput => DistributionStatus.Singular,
            LinearAlgebraStatus.Singular => DistributionStatus.Singular,
            LinearAlgebraStatus.RankDeficient => DistributionStatus.Singular,
            LinearAlgebraStatus.NotPositiveDefinite =>
                DistributionStatus.NotPositiveDefinite,
            LinearAlgebraStatus.NonFiniteOutput => DistributionStatus.NonFiniteOutput,
            LinearAlgebraStatus.NumericalFailure => DistributionStatus.NumericalFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown linear algebra status."),
        };
    }

}
