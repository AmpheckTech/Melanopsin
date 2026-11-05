// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable real tridiagonal matrix in compact diagonal storage.
/// </summary>
/// <remarks>
/// The three diagonals occupy <c>O(n)</c> storage and matrix-vector products
/// and solves take <c>O(n)</c> time. <see cref="TrySolve(ReadOnlySpan{double}, Span{double}, Span{double})"/>
/// uses the Thomas algorithm without pivoting. It fails on an exactly zero
/// pivot but does not classify near-zero pivots as singular; callers that need
/// a condition estimate should use a decomposition that exposes singular
/// values.
/// </remarks>
public sealed class TridiagonalMatrix {
    private readonly double[] _subDiagonal;
    private readonly double[] _diagonal;
    private readonly double[] _superDiagonal;

    /// <summary>
    /// Creates a tridiagonal matrix from its three diagonals.
    /// </summary>
    /// <param name="subDiagonal">The entries below the main diagonal.</param>
    /// <param name="diagonal">The entries on the main diagonal.</param>
    /// <param name="superDiagonal">The entries above the main diagonal.</param>
    public TridiagonalMatrix(
        ReadOnlySpan<double> subDiagonal,
        ReadOnlySpan<double> diagonal,
        ReadOnlySpan<double> superDiagonal) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diagonal.Length);
        if (subDiagonal.Length != diagonal.Length - 1) {
            throw new ArgumentException(
                "The subdiagonal must contain order - 1 values.",
                nameof(subDiagonal));
        }

        if (superDiagonal.Length != diagonal.Length - 1) {
            throw new ArgumentException(
                "The superdiagonal must contain order - 1 values.",
                nameof(superDiagonal));
        }

        ValidateFinite(subDiagonal, nameof(subDiagonal));
        ValidateFinite(diagonal, nameof(diagonal));
        ValidateFinite(superDiagonal, nameof(superDiagonal));
        _subDiagonal = subDiagonal.ToArray();
        _diagonal = diagonal.ToArray();
        _superDiagonal = superDiagonal.ToArray();
    }

    /// <summary>Gets the matrix order.</summary>
    public int Order => _diagonal.Length;

    /// <summary>Gets the entries below the main diagonal.</summary>
    public ReadOnlySpan<double> SubDiagonal => _subDiagonal;

    /// <summary>Gets the main diagonal.</summary>
    public ReadOnlySpan<double> Diagonal => _diagonal;

    /// <summary>Gets the entries above the main diagonal.</summary>
    public ReadOnlySpan<double> SuperDiagonal => _superDiagonal;

    /// <summary>Gets the sum of the main diagonal.</summary>
    public double Trace {
        get {
            var trace = 0.0;
            for (var index = 0; index < _diagonal.Length; index++) {
                trace += _diagonal[index];
            }

            return trace;
        }
    }

    /// <summary>Gets an entry by zero-based row and column.</summary>
    public double this[int row, int column] {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            if (row >= Order || column >= Order) {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (column == row) {
                return _diagonal[row];
            }

            if (column == row - 1) {
                return _subDiagonal[row - 1];
            }

            return column == row + 1 ? _superDiagonal[row] : 0;
        }
    }

    /// <summary>
    /// Computes the determinant with the tridiagonal recurrence.
    /// </summary>
    /// <remarks>
    /// The recurrence has <c>O(n)</c> time and constant extra space. A finite
    /// result is not a singularity verdict; very large or small determinants
    /// can overflow or underflow for any given matrix scaling.
    /// </remarks>
    public double Determinant {
        get {
            if (Order == 1) {
                return _diagonal[0];
            }

            var previousPrevious = _diagonal[0];
            var previous = (_diagonal[1] * previousPrevious) -
                (_subDiagonal[0] * _superDiagonal[0]);
            for (var index = 2; index < Order; index++) {
                var current = (_diagonal[index] * previous) -
                    (_subDiagonal[index - 1] * _superDiagonal[index - 1] *
                        previousPrevious);
                previousPrevious = previous;
                previous = current;
            }

            return previous;
        }
    }

    /// <summary>Copies the full row-major matrix to a destination.</summary>
    public void CopyToDense(Span<double> destination) {
        var elementCount = checked(Order * Order);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var index = 0; index < Order; index++) {
            destination[(index * Order) + index] = _diagonal[index];
            if (index + 1 < Order) {
                destination[(index * Order) + index + 1] =
                    _superDiagonal[index];
                destination[((index + 1) * Order) + index] =
                    _subDiagonal[index];
            }
        }
    }

    /// <summary>Returns the full row-major matrix.</summary>
    public double[] ToDense() {
        var destination = new double[checked(Order * Order)];
        CopyToDense(destination);
        return destination;
    }

    /// <summary>Multiplies this matrix by a vector.</summary>
    public void Multiply(ReadOnlySpan<double> vector, Span<double> destination) {
        if (vector.Length < Order) {
            throw new ArgumentException(
                "The vector does not contain enough values.",
                nameof(vector));
        }

        if (destination.Length < Order) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (vector.Overlaps(destination)) {
            throw new ArgumentException(
                "The vector and destination must not overlap.",
                nameof(destination));
        }

        for (var row = 0; row < Order; row++) {
            var value = _diagonal[row] * vector[row];
            if (row > 0) {
                value += _subDiagonal[row - 1] * vector[row - 1];
            }

            if (row + 1 < Order) {
                value += _superDiagonal[row] * vector[row + 1];
            }

            destination[row] = value;
        }
    }

    /// <summary>
    /// Solves a system with the Thomas algorithm using caller-owned workspace.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Order"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Order"/> values.</param>
    /// <param name="workspace">At least <see cref="Order"/> values.</param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the solution is cleared.
    /// </returns>
    public bool TrySolve(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace) {
        return TrySolve(rightHandSide, solution, workspace, out _);
    }

    /// <summary>
    /// Solves a tridiagonal system and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Order"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Order"/> values.</param>
    /// <param name="workspace">At least <see cref="Order"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TrySolve(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out LinearAlgebraOperationReport report) {
        if (rightHandSide.Length < Order) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (solution.Length < Order) {
            throw new ArgumentException(
                "The solution does not contain enough values.",
                nameof(solution));
        }

        if (workspace.Length < Order) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(workspace));
        }

        if (solution.Overlaps(rightHandSide) || solution.Overlaps(workspace) ||
            workspace.Overlaps(rightHandSide)) {
            throw new ArgumentException(
                "The right-hand side, solution, and workspace must not overlap.",
                nameof(solution));
        }

        for (var index = 0; index < Order; index++) {
            if (!double.IsFinite(rightHandSide[index])) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.TridiagonalSolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }
        }

        var order = Order;
        if (_diagonal[0] == 0) {
            solution[..order].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.TridiagonalSolve,
                LinearAlgebraStatus.Singular);
            return false;
        }

        workspace[0] = _superDiagonal.Length == 0
            ? 0
            : _superDiagonal[0] / _diagonal[0];
        solution[0] = rightHandSide[0] / _diagonal[0];
        for (var index = 1; index < order - 1; index++) {
            var pivot = _diagonal[index] -
                (_subDiagonal[index - 1] * workspace[index - 1]);
            if (pivot == 0) {
                solution[..order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.TridiagonalSolve,
                    LinearAlgebraStatus.Singular);
                return false;
            }

            workspace[index] = _superDiagonal[index] / pivot;
            solution[index] = (rightHandSide[index] -
                (_subDiagonal[index - 1] * solution[index - 1])) / pivot;
        }

        if (order > 1) {
            var last = order - 1;
            var pivot = _diagonal[last] -
                (_subDiagonal[last - 1] * workspace[last - 1]);
            if (pivot == 0) {
                solution[..order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.TridiagonalSolve,
                    LinearAlgebraStatus.Singular);
                return false;
            }

            solution[last] = (rightHandSide[last] -
                (_subDiagonal[last - 1] * solution[last - 2])) / pivot;
            for (var index = order - 2; index >= 0; index--) {
                solution[index] -= workspace[index] * solution[index + 1];
            }
        }

        for (var index = 0; index < order; index++) {
            if (!double.IsFinite(solution[index])) {
                solution[..order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.TridiagonalSolve,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.TridiagonalSolve,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>Solves a tridiagonal system.</summary>
    public double[] Solve(ReadOnlySpan<double> rightHandSide) {
        var solution = new double[Order];
        var workspace = new double[Order];
        if (!TrySolve(rightHandSide, solution, workspace)) {
            throw new ArithmeticException(
                "The tridiagonal system has a zero pivot or a non-finite solution.");
        }

        return solution;
    }

    private static void ValidateFinite(
        ReadOnlySpan<double> values,
        string parameterName) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "The diagonal values must be finite.",
                    parameterName);
            }
        }
    }
}
