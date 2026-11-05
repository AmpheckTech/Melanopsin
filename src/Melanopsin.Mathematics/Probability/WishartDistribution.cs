namespace Melanopsin.Mathematics;

/// <summary>
/// A Wishart distribution over real symmetric positive-definite matrices.
/// </summary>
/// <remarks>
/// The scale matrix uses row-major storage and must be symmetric
/// positive-definite. Densities are evaluated without forming the scale-matrix
/// inverse or the observation inverse. The degrees of freedom must exceed
/// <c>Dimension - 1</c>; no absolute determinant threshold is used. The
/// distribution is the one introduced by Wishart (1928),
/// <see href="https://doi.org/10.1093/biomet/20A.1-2.32">doi:10.1093/biomet/20A.1-2.32</see>.
/// </remarks>
public sealed class WishartDistribution {
    private readonly double[] _scale;
    private readonly double[] _lowerScale;
    private readonly double _logNormalization;
    private readonly double _symmetryTolerance;

    public WishartDistribution(
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
        _lowerScale = new double[elementCount];
        if (!WishartDistributionMath.TryFactor(
            _scale,
            dimension,
            relativeSymmetryTolerance,
            _lowerScale,
            out var logDeterminant)) {
            throw new ArgumentException(
                "The scale matrix must be finite, symmetric, and positive definite.",
                nameof(scale));
        }

        Dimension = dimension;
        DegreesOfFreedom = degreesOfFreedom;
        _symmetryTolerance = relativeSymmetryTolerance;
        _logNormalization =
            (-0.5 * degreesOfFreedom * dimension * Constants.Ln2) -
            (0.5 * degreesOfFreedom * logDeterminant) -
            SpecialFunctions.LogMultivariateGamma(
                0.5 * degreesOfFreedom,
                dimension);
    }

    public int Dimension { get; }

    public double DegreesOfFreedom { get; }

    public ReadOnlySpan<double> Scale => _scale;

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
    /// Receives the log density, including negative infinity for a matrix
    /// outside the positive-definite support.
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
            rightHandSide.Clear();
            rightHandSide[column] = 1;
            if (!WishartDistributionMath.TrySolveCholesky(
                _lowerScale,
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

            for (var row = 0; row < dimension; row++) {
                trace += solution[row] * value[(row * dimension) + column];
            }
        }

        result = (0.5 * (DegreesOfFreedom - dimension - 1) * logDeterminant) -
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
    /// Writes the mean matrix <c>degreesOfFreedom * scale</c>.
    /// </summary>
    public void CopyMeanTo(Span<double> destination) {
        var elementCount = checked(_scale.Length);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        var scale = DegreesOfFreedom;
        for (var index = 0; index < elementCount; index++) {
            destination[index] = scale * _scale[index];
        }
    }

    /// <summary>
    /// Draws a matrix using the Bartlett decomposition.
    /// </summary>
    public double[] Sample(ref Xoshiro256StarStar generator) {
        var elementCount = checked(Dimension * Dimension);
        var result = new double[elementCount];
        var workspace = new double[2 * elementCount];
        if (!TrySample(result, workspace, ref generator)) {
            throw new ArithmeticException(
                "The Wishart sample could not be represented as finite values.");
        }

        return result;
    }

    /// <summary>
    /// Draws a matrix using the Bartlett decomposition without allocating.
    /// </summary>
    /// <param name="destination">
    /// Receives <c>Dimension * Dimension</c> row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least <c>2 * Dimension * Dimension</c> values, not overlapping
    /// <paramref name="destination"/>.
    /// </param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <returns>
    /// <see langword="false"/> if a non-finite sample is produced.
    /// </returns>
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
    /// Draws a Wishart matrix and returns a diagnostic report.
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

        if (workspace.Length < 2 * elementCount) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (destination[..elementCount].Overlaps(workspace)) {
            throw new ArgumentException(
                "The destination and workspace must not overlap.",
                nameof(workspace));
        }

        var bartlett = workspace[..elementCount];
        var product = workspace.Slice(elementCount, elementCount);
        for (var row = 0; row < dimension; row++) {
            var rowOffset = row * dimension;
            for (var column = 0; column <= row; column++) {
                double value;
                if (row == column) {
                    var shape = 0.5 * (DegreesOfFreedom - column);
                    var gamma = DistributionValidation.SampleStandardGamma(
                        ref generator,
                        shape);
                    value = Math.Sqrt(2 * gamma);
                } else {
                    value = DistributionValidation.SampleNormal(ref generator);
                }

                bartlett[rowOffset + column] = value;
            }
        }

        WishartDistributionMath.MultiplyLowerByLower(
            _lowerScale,
            bartlett,
            product,
            dimension);
        WishartDistributionMath.SquareLowerProduct(
            product,
            destination,
            dimension);
        if (!IsFinite(destination, elementCount)) {
            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

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

internal static class WishartDistributionMath {
    public static bool TryFactor(
        ReadOnlySpan<double> matrix,
        int dimension,
        double relativeSymmetryTolerance,
        Span<double> lower,
        out double logDeterminant) {
        return TryFactor(
            matrix,
            dimension,
            relativeSymmetryTolerance,
            lower,
            out logDeterminant,
            out _);
    }

    public static bool TryFactor(
        ReadOnlySpan<double> matrix,
        int dimension,
        double relativeSymmetryTolerance,
        Span<double> lower,
        out double logDeterminant,
        out DistributionOperationReport report) {
        var elementCount = checked(dimension * dimension);
        if (matrix.Length < elementCount || lower.Length < elementCount) {
            throw new ArgumentException(
                "A matrix buffer is too small.");
        }

        if (matrix[..elementCount].Overlaps(lower)) {
            throw new ArgumentException(
                "The source and lower-factor buffers must not overlap.",
                nameof(lower));
        }

        var scale = 0.0;
        for (var index = 0; index < elementCount; index++) {
            var value = matrix[index];
            if (!double.IsFinite(value)) {
                logDeterminant = double.NaN;
                report = new DistributionOperationReport(
                    DistributionOperation.MatrixFactorization,
                    DistributionStatus.NonFiniteInput);
                return false;
            }

            scale = Math.Max(scale, Math.Abs(value));
        }

        for (var row = 0; row < dimension; row++) {
            for (var column = row + 1; column < dimension; column++) {
                var asymmetry = Math.Abs(
                    matrix[(row * dimension) + column] -
                    matrix[(column * dimension) + row]);
                if (asymmetry > relativeSymmetryTolerance * scale) {
                    logDeterminant = double.NaN;
                    report = new DistributionOperationReport(
                        DistributionOperation.MatrixFactorization,
                        DistributionStatus.InvalidInput);
                    return false;
                }
            }
        }

        lower[..elementCount].Clear();
        logDeterminant = 0;
        for (var row = 0; row < dimension; row++) {
            var rowOffset = row * dimension;
            for (var column = 0; column <= row; column++) {
                var value = matrix[rowOffset + column];
                var columnOffset = column * dimension;
                for (var inner = 0; inner < column; inner++) {
                    value -= lower[rowOffset + inner] *
                        lower[columnOffset + inner];
                }

                if (row == column) {
                    if (!(value > 0)) {
                        logDeterminant = double.NaN;
                        lower[..elementCount].Clear();
                        report = new DistributionOperationReport(
                            DistributionOperation.MatrixFactorization,
                            DistributionStatus.NotPositiveDefinite);
                        return false;
                    }

                    if (!double.IsFinite(value)) {
                        logDeterminant = double.NaN;
                        lower[..elementCount].Clear();
                        report = new DistributionOperationReport(
                            DistributionOperation.MatrixFactorization,
                            DistributionStatus.NonFiniteOutput);
                        return false;
                    }

                    var diagonal = Math.Sqrt(value);
                    lower[rowOffset + column] = diagonal;
                    logDeterminant += 2 * Math.Log(diagonal);
                } else {
                    lower[rowOffset + column] =
                        value / lower[columnOffset + column];
                }
            }
        }

        if (!double.IsFinite(logDeterminant)) {
            lower[..elementCount].Clear();
            report = new DistributionOperationReport(
                DistributionOperation.MatrixFactorization,
                DistributionStatus.NonFiniteOutput);
            return false;
        }

        report = new DistributionOperationReport(
            DistributionOperation.MatrixFactorization,
            DistributionStatus.Succeeded);
        return true;
    }

    public static bool TrySolveCholesky(
        ReadOnlySpan<double> lower,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        int dimension) {
        return TrySolveCholesky(
            lower,
            rightHandSide,
            solution,
            dimension,
            out _);
    }

    public static bool TrySolveCholesky(
        ReadOnlySpan<double> lower,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        int dimension,
        out DistributionOperationReport report) {
        var elementCount = checked(dimension * dimension);
        if (lower.Length < elementCount ||
            rightHandSide.Length < dimension ||
            solution.Length < dimension) {
            throw new ArgumentException("A solve buffer is too small.");
        }

        if (rightHandSide[..dimension].Overlaps(solution[..dimension])) {
            throw new ArgumentException(
                "The solution must not overlap the right-hand side.",
                nameof(solution));
        }

        for (var index = 0; index < dimension; index++) {
            if (!double.IsFinite(rightHandSide[index])) {
                solution[..dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.MatrixSolve,
                    DistributionStatus.NonFiniteInput);
                return false;
            }
        }

        for (var row = 0; row < dimension; row++) {
            var value = rightHandSide[row];
            for (var column = 0; column < row; column++) {
                value -= lower[(row * dimension) + column] * solution[column];
            }

            var diagonal = lower[(row * dimension) + row];
            if (diagonal == 0) {
                solution[..dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.MatrixSolve,
                    DistributionStatus.Singular);
                return false;
            }

            solution[row] = value / diagonal;
        }

        for (var row = dimension - 1; row >= 0; row--) {
            var value = solution[row];
            for (var column = row + 1; column < dimension; column++) {
                value -= lower[(column * dimension) + row] * solution[column];
            }

            var diagonal = lower[(row * dimension) + row];
            if (diagonal == 0) {
                solution[..dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.MatrixSolve,
                    DistributionStatus.Singular);
                return false;
            }

            solution[row] = value / diagonal;
            if (!double.IsFinite(solution[row])) {
                solution[..dimension].Clear();
                report = new DistributionOperationReport(
                    DistributionOperation.MatrixSolve,
                    DistributionStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new DistributionOperationReport(
            DistributionOperation.MatrixSolve,
            DistributionStatus.Succeeded);
        return true;
    }

    public static bool TryInvertSpd(
        ReadOnlySpan<double> lower,
        Span<double> inverse,
        Span<double> workspace,
        int dimension) {
        return TryInvertSpd(
            lower,
            inverse,
            workspace,
            dimension,
            out _);
    }

    public static bool TryInvertSpd(
        ReadOnlySpan<double> lower,
        Span<double> inverse,
        Span<double> workspace,
        int dimension,
        out DistributionOperationReport report) {
        var elementCount = checked(dimension * dimension);
        if (lower.Length < elementCount || inverse.Length < elementCount) {
            throw new ArgumentException("A matrix buffer is too small.");
        }

        if (workspace.Length < dimension) {
            throw new ArgumentException("The workspace is too small.");
        }

        if (inverse[..elementCount].Overlaps(workspace[..dimension])) {
            throw new ArgumentException(
                "The inverse and workspace must not overlap.",
                nameof(workspace));
        }

        inverse[..elementCount].Clear();
        for (var index = 0; index < dimension; index++) {
            inverse[(index * dimension) + index] = 1;
        }

        var solution = workspace[..dimension];
        for (var column = 0; column < dimension; column++) {
            for (var row = 0; row < dimension; row++) {
                var value = inverse[(row * dimension) + column];
                for (var inner = 0; inner < row; inner++) {
                    value -= lower[(row * dimension) + inner] *
                        solution[inner];
                }

                var diagonal = lower[(row * dimension) + row];
                if (diagonal == 0) {
                    inverse[..elementCount].Clear();
                    report = new DistributionOperationReport(
                        DistributionOperation.MatrixInversion,
                        DistributionStatus.Singular);
                    return false;
                }

                solution[row] = value / diagonal;
            }

            for (var row = dimension - 1; row >= 0; row--) {
                var value = solution[row];
                for (var inner = row + 1; inner < dimension; inner++) {
                    value -= lower[(inner * dimension) + row] *
                        solution[inner];
                }

                var diagonal = lower[(row * dimension) + row];
                if (diagonal == 0) {
                    inverse[..elementCount].Clear();
                    report = new DistributionOperationReport(
                        DistributionOperation.MatrixInversion,
                        DistributionStatus.Singular);
                    return false;
                }

                solution[row] = value / diagonal;
                if (!double.IsFinite(solution[row])) {
                    inverse[..elementCount].Clear();
                    report = new DistributionOperationReport(
                        DistributionOperation.MatrixInversion,
                        DistributionStatus.NonFiniteOutput);
                    return false;
                }

                inverse[(row * dimension) + column] = solution[row];
            }
        }

        report = new DistributionOperationReport(
            DistributionOperation.MatrixInversion,
            DistributionStatus.Succeeded);
        return true;
    }

    public static void MultiplyLowerByLower(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right,
        Span<double> destination,
        int dimension) {
        var elementCount = checked(dimension * dimension);
        destination[..elementCount].Clear();
        for (var row = 0; row < dimension; row++) {
            for (var column = 0; column <= row; column++) {
                var value = 0.0;
                for (var inner = column; inner <= row; inner++) {
                    value += left[(row * dimension) + inner] *
                        right[(inner * dimension) + column];
                }

                destination[(row * dimension) + column] = value;
            }
        }
    }

    public static void SquareLowerProduct(
        ReadOnlySpan<double> lowerProduct,
        Span<double> destination,
        int dimension) {
        var elementCount = checked(dimension * dimension);
        destination[..elementCount].Clear();
        for (var row = 0; row < dimension; row++) {
            for (var column = 0; column <= row; column++) {
                var value = 0.0;
                for (var inner = 0; inner <= column; inner++) {
                    value += lowerProduct[(row * dimension) + inner] *
                        lowerProduct[(column * dimension) + inner];
                }

                destination[(row * dimension) + column] = value;
                destination[(column * dimension) + row] = value;
            }
        }
    }
}
