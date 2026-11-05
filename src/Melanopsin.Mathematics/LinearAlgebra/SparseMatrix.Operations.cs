// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public sealed partial class SparseMatrix {
    /// <summary>Creates a square sparse identity matrix.</summary>
    public static SparseMatrix CreateIdentity(int order) {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        var rowOffsets = new int[checked(order + 1)];
        var columnIndices = new int[order];
        var values = new double[order];
        for (var index = 0; index < order; index++) {
            rowOffsets[index] = index;
            columnIndices[index] = index;
            values[index] = 1;
        }

        rowOffsets[order] = order;
        return new SparseMatrix(
            rowOffsets,
            columnIndices,
            values,
            order,
            order);
    }

    /// <summary>
    /// Creates a rectangular sparse matrix whose nonzero entries lie on the
    /// main diagonal.
    /// </summary>
    public static SparseMatrix CreateDiagonal(ReadOnlySpan<double> diagonal) {
        var order = diagonal.Length;
        var rowOffsets = new int[checked(order + 1)];
        var columnIndices = new int[order];
        var values = new double[order];
        var target = 0;
        for (var index = 0; index < order; index++) {
            rowOffsets[index] = target;
            var value = diagonal[index];
            if (!double.IsFinite(value)) {
                throw new ArgumentException(
                    "The diagonal must contain only finite values.",
                    nameof(diagonal));
            }

            if (value != 0) {
                columnIndices[target] = index;
                values[target] = value;
                target++;
            }
        }

        rowOffsets[order] = target;
        return new SparseMatrix(
            rowOffsets,
            columnIndices.AsSpan(0, target),
            values.AsSpan(0, target),
            order,
            order);
    }

    /// <summary>Gets the main diagonal entries as a dense vector.</summary>
    public double[] Diagonal() {
        var diagonalCount = Math.Min(RowCount, ColumnCount);
        var result = new double[diagonalCount];
        for (var row = 0; row < diagonalCount; row++) {
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                var column = _columnIndices[index];
                if (column == row) {
                    result[row] = _values[index];
                    break;
                }

                if (column > row) {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Computes <c>this * scale</c> as a canonical sparse matrix.
    /// </summary>
    public SparseMatrix Scale(double scale) {
        if (!double.IsFinite(scale)) {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        if (scale == 0) {
            return new SparseMatrix(
                new int[checked(RowCount + 1)],
                [],
                [],
                RowCount,
                ColumnCount);
        }

        var values = new double[_values.Length];
        for (var index = 0; index < values.Length; index++) {
            values[index] = _values[index] * scale;
            if (!double.IsFinite(values[index])) {
                throw new ArithmeticException(
                    "Sparse scaling produced a non-finite value.");
            }
        }

        return new SparseMatrix(
            _rowOffsets,
            _columnIndices,
            values,
            RowCount,
            ColumnCount);
    }

    /// <summary>
    /// Computes the sum of two matrices with equal dimensions.
    /// </summary>
    public SparseMatrix Add(SparseMatrix other) {
        ArgumentNullException.ThrowIfNull(other);
        if (RowCount != other.RowCount || ColumnCount != other.ColumnCount) {
            throw new ArgumentException(
                "Matrices must have equal dimensions.",
                nameof(other));
        }

        var rowOffsets = new int[checked(RowCount + 1)];
        var columnIndices = new List<int>(_values.Length + other._values.Length);
        var values = new List<double>(_values.Length + other._values.Length);
        for (var row = 0; row < RowCount; row++) {
            rowOffsets[row] = values.Count;
            var firstIndex = _rowOffsets[row];
            var firstEnd = _rowOffsets[row + 1];
            var secondIndex = other._rowOffsets[row];
            var secondEnd = other._rowOffsets[row + 1];
            while (firstIndex < firstEnd || secondIndex < secondEnd) {
                int column;
                double value;
                if (secondIndex >= secondEnd ||
                    (firstIndex < firstEnd &&
                        _columnIndices[firstIndex] <
                            other._columnIndices[secondIndex])) {
                    column = _columnIndices[firstIndex];
                    value = _values[firstIndex++];
                } else if (firstIndex >= firstEnd ||
                    other._columnIndices[secondIndex] <
                        _columnIndices[firstIndex]) {
                    column = other._columnIndices[secondIndex];
                    value = other._values[secondIndex++];
                } else {
                    column = _columnIndices[firstIndex];
                    value = _values[firstIndex++] + other._values[secondIndex++];
                }

                if (!double.IsFinite(value)) {
                    throw new ArithmeticException(
                        "Sparse addition produced a non-finite value.");
                }

                if (value != 0) {
                    columnIndices.Add(column);
                    values.Add(value);
                }
            }
        }

        rowOffsets[RowCount] = values.Count;
        return new SparseMatrix(
            rowOffsets,
            columnIndices.ToArray(),
            values.ToArray(),
            RowCount,
            ColumnCount);
    }

    /// <summary>
    /// Computes <c>this * other</c> with a one-row accumulator. The temporary
    /// storage grows with the output column count rather than its nonzero
    /// count.
    /// </summary>
    public SparseMatrix Multiply(SparseMatrix other) {
        ArgumentNullException.ThrowIfNull(other);
        if (ColumnCount != other.RowCount) {
            throw new ArgumentException(
                "The left column count must equal the right row count.",
                nameof(other));
        }

        var rowOffsets = new int[checked(RowCount + 1)];
        var columnIndices = new List<int>();
        var values = new List<double>();
        var accumulated = new double[other.ColumnCount];
        var stamps = new int[other.ColumnCount];
        var touched = new List<int>();
        for (var row = 0; row < RowCount; row++) {
            rowOffsets[row] = values.Count;
            touched.Clear();
            var stamp = row + 1;
            for (var leftIndex = _rowOffsets[row];
                leftIndex < _rowOffsets[row + 1];
                leftIndex++) {
                var leftValue = _values[leftIndex];
                var otherRow = _columnIndices[leftIndex];
                for (var rightIndex = other._rowOffsets[otherRow];
                    rightIndex < other._rowOffsets[otherRow + 1];
                    rightIndex++) {
                    var column = other._columnIndices[rightIndex];
                    if (stamps[column] != stamp) {
                        stamps[column] = stamp;
                        accumulated[column] = 0;
                        touched.Add(column);
                    }

                    accumulated[column] +=
                        leftValue * other._values[rightIndex];
                }
            }

            touched.Sort();
            foreach (var column in touched) {
                var value = accumulated[column];
                accumulated[column] = 0;
                if (!double.IsFinite(value)) {
                    throw new ArithmeticException(
                        "Sparse multiplication produced a non-finite value.");
                }

                if (value != 0) {
                    columnIndices.Add(column);
                    values.Add(value);
                }
            }
        }

        rowOffsets[RowCount] = values.Count;
        return new SparseMatrix(
            rowOffsets,
            columnIndices.ToArray(),
            values.ToArray(),
            RowCount,
            other.ColumnCount);
    }

    /// <summary>
    /// Solves a general square system with the BiCGSTAB method, starting from
    /// a zero initial guess.
    /// </summary>
    /// <remarks>
    /// The matrix is not required to be symmetric or positive definite. The
    /// residual convergence test is
    /// <c>absoluteTolerance + relativeTolerance * ||rhs||</c>. Exact breakdown
    /// in a scalar inner product, non-finite arithmetic, or exhaustion of the
    /// iteration budget returns false. Near-singularity is not classified by an
    /// absolute threshold. The workspace requires six vectors of order values.
    /// </remarks>
    public bool TryBiConjugateGradientStabilized(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryBiConjugateGradientStabilized(
            rightHandSide,
            solution,
            workspace,
            out SparseIterativeReport report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        iterationCount = report.IterationCount;
        return succeeded;
    }

    private bool TryBiConjugateGradientStabilizedCore(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        out SparseIterativeStatus status,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ValidateIterativeSolveArguments(
            rightHandSide,
            solution,
            workspace,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        var order = RowCount;
        if (workspace.Length < checked(order * 6)) {
            throw new ArgumentException(
                "The workspace must contain at least six times the order.",
                nameof(workspace));
        }

        var residual = workspace[..order];
        var shadowResidual = workspace.Slice(order, order);
        var direction = workspace.Slice(order * 2, order);
        var product = workspace.Slice(order * 3, order);
        var intermediateResidual = workspace.Slice(order * 4, order);
        var intermediateProduct = workspace.Slice(order * 5, order);
        solution[..order].Clear();
        var rightHandSideNormSquared = 0.0;
        for (var index = 0; index < order; index++) {
            var value = rightHandSide[index];
            if (!double.IsFinite(value)) {
                throw new ArgumentException(
                    "The right-hand side must contain only finite values.",
                    nameof(rightHandSide));
            }

            residual[index] = value;
            shadowResidual[index] = value;
            rightHandSideNormSquared += value * value;
        }

        var tolerance = absoluteTolerance +
            (relativeTolerance * Math.Sqrt(rightHandSideNormSquared));
        if (Math.Sqrt(rightHandSideNormSquared) <= tolerance) {
            iterationCount = 0;
            status = SparseIterativeStatus.Succeeded;
            return true;
        }

        var previousRho = 1.0;
        var alpha = 1.0;
        var omega = 1.0;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            var rho = Dot(shadowResidual, residual);
            if (!double.IsFinite(rho)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (rho == 0) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            var beta = (rho / previousRho) * (alpha / omega);
            if (!double.IsFinite(beta)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            for (var index = 0; index < order; index++) {
                direction[index] = residual[index] +
                    (beta * (direction[index] - (omega * product[index])));
            }

            Multiply(direction, product);
            var denominator = Dot(shadowResidual, product);
            if (!double.IsFinite(denominator)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (denominator == 0) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            alpha = rho / denominator;
            if (!double.IsFinite(alpha)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            for (var index = 0; index < order; index++) {
                intermediateResidual[index] =
                    residual[index] - (alpha * product[index]);
            }

            var intermediateResidualNormSquared = NormSquared(intermediateResidual);
            if (!double.IsFinite(intermediateResidualNormSquared)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (intermediateResidualNormSquared <= tolerance * tolerance) {
                if (!AddScaled(solution, direction, alpha)) {
                    iterationCount = iteration - 1;
                    status = SparseIterativeStatus.NonFiniteArithmetic;
                    return false;
                }

                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }

            Multiply(intermediateResidual, intermediateProduct);
            var intermediateDot = Dot(intermediateProduct, intermediateResidual);
            var intermediateNormSquared = NormSquared(intermediateProduct);
            if (!double.IsFinite(intermediateDot) ||
                !double.IsFinite(intermediateNormSquared)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (intermediateNormSquared == 0) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            omega = intermediateDot / intermediateNormSquared;
            if (!double.IsFinite(omega)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (!AddScaled(solution, direction, alpha) ||
                !AddScaled(solution, intermediateResidual, omega)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            for (var index = 0; index < order; index++) {
                residual[index] = intermediateResidual[index] -
                    (omega * intermediateProduct[index]);
            }

            var residualNormSquared = NormSquared(residual);
            if (!double.IsFinite(residualNormSquared)) {
                iterationCount = iteration;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (residualNormSquared <= tolerance * tolerance) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }

            if (omega == 0) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            previousRho = rho;
        }

        iterationCount = maximumIterations;
        status = SparseIterativeStatus.MaximumIterationsReached;
        return false;
    }

    private void ValidateIterativeSolveArguments(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        if (RowCount != ColumnCount) {
            throw new InvalidOperationException(
                "The matrix must be square.");
        }

        if (rightHandSide.Length < RowCount) {
            throw new ArgumentException(
                "The right-hand side is too small.",
                nameof(rightHandSide));
        }

        if (solution.Length < RowCount) {
            throw new ArgumentException(
                "The solution is too small.",
                nameof(solution));
        }

        if (rightHandSide.Overlaps(solution) ||
            rightHandSide.Overlaps(workspace) ||
            solution.Overlaps(workspace)) {
            throw new ArgumentException(
                "The right-hand side, solution, and workspace must not overlap.",
                nameof(workspace));
        }

        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
    }

    private static double Dot(ReadOnlySpan<double> left, ReadOnlySpan<double> right) {
        var result = 0.0;
        for (var index = 0; index < left.Length; index++) {
            result += left[index] * right[index];
        }

        return result;
    }

    private static double NormSquared(ReadOnlySpan<double> values) {
        return Dot(values, values);
    }

    private static bool AddScaled(
        Span<double> destination,
        ReadOnlySpan<double> source,
        double scale) {
        for (var index = 0; index < destination.Length; index++) {
            destination[index] += scale * source[index];
            if (!double.IsFinite(destination[index])) {
                return false;
            }
        }

        return true;
    }
}
