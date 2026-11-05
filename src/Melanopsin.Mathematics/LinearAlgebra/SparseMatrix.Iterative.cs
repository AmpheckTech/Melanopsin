// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public sealed partial class SparseMatrix {
    /// <summary>Solves a square system with the Jacobi iteration.</summary>
    public bool TryJacobi(
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryJacobi(
            rightHandSide,
            initialGuess,
            solution,
            workspace,
            out SparseIterativeReport report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        iterationCount = report.IterationCount;
        return succeeded;
    }

    /// <summary>
    /// Solves a square system with the Jacobi iteration.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="initialGuess">The finite initial iterate.</param>
    /// <param name="solution">
    /// Receives the final iterate. It must not overlap any input or
    /// <paramref name="workspace"/>.
    /// </param>
    /// <param name="workspace">
    /// At least <c>order</c> values. The first <c>order</c> entries are used
    /// as the next-iterate buffer.
    /// </param>
    /// <param name="iterationCount">The number of completed iterations.</param>
    /// <param name="status">Receives the numerical or iteration termination status.</param>
    /// <param name="absoluteTolerance">
    /// The absolute part of the infinity-norm residual threshold.
    /// </param>
    /// <param name="relativeTolerance">
    /// The scale-relative part of the infinity-norm residual threshold.
    /// </param>
    /// <param name="maximumIterations">The maximum number of iterations.</param>
    /// <remarks>
    /// The iteration requires a present, exactly nonzero diagonal entry in
    /// every row. A structural zero diagonal returns false before iterating.
    /// Convergence uses the infinity norm and the absolute plus relative
    /// residual criterion
    /// <c>absoluteTolerance + relativeTolerance * max(abs(rhs))</c>. No
    /// near-singularity threshold is applied. On non-convergence, the solution
    /// contains the latest finite iterate; on non-finite arithmetic it contains
    /// the last complete finite iterate.
    /// </remarks>
    private bool TryJacobiCore(
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        out SparseIterativeStatus status,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ValidateStationaryIterationArguments(
            rightHandSide,
            solution,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        if (initialGuess.Length < RowCount) {
            throw new ArgumentException(
                "The initial guess is too small.",
                nameof(initialGuess));
        }

        if (workspace.Length < RowCount) {
            throw new ArgumentException(
                "The workspace must contain at least order values.",
                nameof(workspace));
        }

        if (initialGuess.Overlaps(solution) ||
            initialGuess.Overlaps(workspace) ||
            solution.Overlaps(workspace)) {
            throw new ArgumentException(
                "The initial guess, solution, and workspace must not overlap.",
                nameof(workspace));
        }

        ValidateFiniteVector(initialGuess, RowCount, nameof(initialGuess));
        var order = RowCount;
        for (var row = 0; row < order; row++) {
            if (!TryGetDiagonal(row, out _)) {
                iterationCount = 0;
                status = SparseIterativeStatus.StructuralZeroDiagonal;
                return false;
            }
        }

        var state = solution[..order];
        initialGuess[..order].CopyTo(state);
        var next = workspace[..order];
        var threshold = ResidualThreshold(
            rightHandSide,
            absoluteTolerance,
            relativeTolerance);
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            for (var row = 0; row < order; row++) {
                var sum = 0.0;
                var diagonal = 0.0;
                for (var index = _rowOffsets[row];
                    index < _rowOffsets[row + 1];
                    index++) {
                    var column = _columnIndices[index];
                    var value = _values[index];
                    if (column == row) {
                        diagonal = value;
                    } else {
                        sum += value * state[column];
                    }
                }

                var valueNext = (rightHandSide[row] - sum) / diagonal;
                if (!double.IsFinite(valueNext)) {
                    iterationCount = iteration - 1;
                    status = SparseIterativeStatus.NonFiniteArithmetic;
                    return false;
                }

                next[row] = valueNext;
            }

            var residual = InfinityResidual(next, rightHandSide);
            next.CopyTo(state);
            if (!double.IsFinite(residual)) {
                iterationCount = iteration;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (residual <= threshold) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }
        }

        iterationCount = maximumIterations;
        status = SparseIterativeStatus.MaximumIterationsReached;
        return false;
    }

    /// <summary>Solves a square system with the Gauss-Seidel iteration.</summary>
    public bool TryGaussSeidel(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out int iterationCount,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryGaussSeidel(
            rightHandSide,
            solution,
            out SparseIterativeReport report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        iterationCount = report.IterationCount;
        return succeeded;
    }

    /// <summary>
    /// Solves a square system with the Gauss-Seidel iteration.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">
    /// Contains the finite initial iterate on entry and receives the final
    /// iterate. It may be updated before convergence is established.
    /// </param>
    /// <param name="iterationCount">The number of completed iterations.</param>
    /// <param name="status">Receives the numerical or iteration termination status.</param>
    /// <param name="absoluteTolerance">
    /// The absolute part of the infinity-norm residual threshold.
    /// </param>
    /// <param name="relativeTolerance">
    /// The scale-relative part of the infinity-norm residual threshold.
    /// </param>
    /// <param name="maximumIterations">The maximum number of iterations.</param>
    /// <remarks>
    /// The iteration requires a present, exactly nonzero diagonal entry in
    /// every row. A structural zero diagonal returns false before mutating the
    /// solution. Convergence uses the infinity norm and the absolute plus
    /// relative residual criterion
    /// <c>absoluteTolerance + relativeTolerance * max(abs(rhs))</c>. No
    /// near-singularity threshold is applied.
    /// </remarks>
    private bool TryGaussSeidelCore(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out int iterationCount,
        out SparseIterativeStatus status,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ValidateStationaryIterationArguments(
            rightHandSide,
            solution,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ValidateFiniteVector(solution, RowCount, nameof(solution));
        var order = RowCount;
        for (var row = 0; row < order; row++) {
            if (!TryGetDiagonal(row, out _)) {
                iterationCount = 0;
                status = SparseIterativeStatus.StructuralZeroDiagonal;
                return false;
            }
        }

        var state = solution[..order];
        var threshold = ResidualThreshold(
            rightHandSide,
            absoluteTolerance,
            relativeTolerance);
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            for (var row = 0; row < order; row++) {
                var sum = 0.0;
                var diagonal = 0.0;
                for (var index = _rowOffsets[row];
                    index < _rowOffsets[row + 1];
                    index++) {
                    var column = _columnIndices[index];
                    var value = _values[index];
                    if (column == row) {
                        diagonal = value;
                    } else {
                        sum += value * state[column];
                    }
                }

                var next = (rightHandSide[row] - sum) / diagonal;
                if (!double.IsFinite(next)) {
                    iterationCount = iteration - 1;
                    status = SparseIterativeStatus.NonFiniteArithmetic;
                    return false;
                }

                state[row] = next;
            }

            var residual = InfinityResidual(state, rightHandSide);
            if (!double.IsFinite(residual)) {
                iterationCount = iteration;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (residual <= threshold) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }
        }

        iterationCount = maximumIterations;
        status = SparseIterativeStatus.MaximumIterationsReached;
        return false;
    }

    private bool TryGetDiagonal(int row, out double diagonal) {
        for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
            if (_columnIndices[index] == row) {
                diagonal = _values[index];
                return diagonal != 0;
            }

            if (_columnIndices[index] > row) {
                break;
            }
        }

        diagonal = 0;
        return false;
    }

    private double InfinityResidual(
        ReadOnlySpan<double> candidate,
        ReadOnlySpan<double> rightHandSide) {
        var maximum = 0.0;
        for (var row = 0; row < RowCount; row++) {
            var product = 0.0;
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                product += _values[index] * candidate[_columnIndices[index]];
            }

            maximum = Math.Max(maximum, Math.Abs(product - rightHandSide[row]));
        }

        return maximum;
    }

    private static double ResidualThreshold(
        ReadOnlySpan<double> rightHandSide,
        double absoluteTolerance,
        double relativeTolerance) {
        var maximum = 0.0;
        for (var index = 0; index < rightHandSide.Length; index++) {
            maximum = Math.Max(maximum, Math.Abs(rightHandSide[index]));
        }

        return absoluteTolerance + (relativeTolerance * maximum);
    }

    private void ValidateStationaryIterationArguments(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        if (RowCount != ColumnCount) {
            throw new InvalidOperationException(
                "The matrix must be square.");
        }

        if (RowCount == 0) {
            throw new InvalidOperationException(
                "The matrix must contain at least one row.");
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

        if (rightHandSide.Overlaps(solution)) {
            throw new ArgumentException(
                "The right-hand side and solution must not overlap.",
                nameof(solution));
        }

        ValidateFiniteVector(rightHandSide, RowCount, nameof(rightHandSide));
        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
    }

    private static void ValidateFiniteVector(
        ReadOnlySpan<double> values,
        int length,
        string parameterName) {
        for (var index = 0; index < length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "The vector must contain only finite values.",
                    parameterName);
            }
        }
    }
}
