namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable LU factorization of a square double-precision matrix using
/// partial pivoting. The source matrix is interpreted as row-major.
/// </summary>
/// <remarks>
/// The factorization stores a unit-lower-triangular factor and an
/// upper-triangular factor in one flat row-major buffer. A zero pivot marks
/// the factorization as singular. No absolute near-singularity threshold is
/// applied; callers should use residual analysis or a condition-number
/// estimate when accuracy matters. Partial-pivoting LU follows Golub and Van
/// Loan, <i>Matrix Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class LuDecomposition {
    private readonly double[] _lu;
    private readonly int[] _permutation;

    /// <summary>
    /// Factors a square row-major matrix.
    /// </summary>
    /// <param name="source">At least <paramref name="order"/> squared values.</param>
    /// <param name="order">The number of rows and columns.</param>
    /// <exception cref="ArgumentException">
    /// The source is too short or contains a non-finite value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> is not positive.
    /// </exception>
    public LuDecomposition(ReadOnlySpan<double> source, int order) {
        if (order <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                "The matrix order must be positive.");
        }

        var elementCount = checked(order * order);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        Order = order;
        _lu = new double[elementCount];
        _permutation = new int[order];
        for (var index = 0; index < elementCount; index++) {
            var value = source[index];
            if (!double.IsFinite(value)) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }

            _lu[index] = value;
        }

        for (var index = 0; index < order; index++) {
            _permutation[index] = index;
        }

        PermutationSign = 1;
        Factor();
        ValidateFactor();
        Determinant = ComputeDeterminant();
    }

    public int Order { get; }

    /// <summary>
    /// Gets +1 for an even permutation and -1 for an odd permutation.
    /// </summary>
    public int PermutationSign { get; private set; }

    public bool IsSingular { get; private set; }

    /// <summary>
    /// Gets the product of the U diagonal and the permutation sign. The value
    /// can overflow or underflow because determinant magnitude is
    /// scale-dependent.
    /// </summary>
    public double Determinant { get; }

    /// <summary>
    /// Solves A x = b using the existing factorization.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Order"/> values.</param>
    /// <param name="solution">
    /// Receives at least <see cref="Order"/> values. This buffer must not
    /// overlap <paramref name="rightHandSide"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the first
    /// <see cref="Order"/> values are cleared.
    /// </returns>
    public bool TrySolve(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution) {
        return TrySolve(rightHandSide, solution, out _);
    }

    /// <summary>
    /// Solves A x = b and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Order"/> values.</param>
    /// <param name="solution">
    /// Receives at least <see cref="Order"/> values. This buffer must not
    /// overlap <paramref name="rightHandSide"/>.
    /// </param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TrySolve(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out LinearAlgebraOperationReport report) {
        if (rightHandSide.Length < Order) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (solution.Length < Order) {
            throw new ArgumentException(
                "The solution buffer does not contain enough values.",
                nameof(solution));
        }

        if (rightHandSide.Overlaps(solution)) {
            throw new ArgumentException(
                "The solution buffer must not overlap the right-hand side.",
                nameof(solution));
        }

        if (IsSingular) {
            solution[..Order].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.LuSolve,
                LinearAlgebraStatus.Singular);
            return false;
        }

        for (var row = 0; row < Order; row++) {
            var value = rightHandSide[_permutation[row]];
            if (!double.IsFinite(value)) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.LuSolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            solution[row] = value;
        }

        for (var row = 1; row < Order; row++) {
            var value = solution[row];
            var rowOffset = row * Order;
            for (var column = 0; column < row; column++) {
                value -= _lu[rowOffset + column] * solution[column];
            }

            solution[row] = value;
        }

        for (var row = Order - 1; row >= 0; row--) {
            var value = solution[row];
            var rowOffset = row * Order;
            for (var column = row + 1; column < Order; column++) {
                value -= _lu[rowOffset + column] * solution[column];
            }

            var pivot = _lu[rowOffset + row];
            if (pivot == 0) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.LuSolve,
                    LinearAlgebraStatus.Singular);
                return false;
            }

            solution[row] = value / pivot;
        }

        for (var index = 0; index < Order; index++) {
            if (!double.IsFinite(solution[index])) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.LuSolve,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.LuSolve,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves A x = b using the existing factorization.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The factorization is singular or the solution is non-finite.
    /// </exception>
    public double[] Solve(ReadOnlySpan<double> rightHandSide) {
        var solution = new double[Order];
        if (!TrySolve(rightHandSide, solution)) {
            throw new ArithmeticException(
                "The system is singular or its solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Computes the inverse by solving against each identity basis vector.
    /// </summary>
    /// <param name="inverse">
    /// Receives <see cref="Order"/> squared row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least <see cref="Order"/> values. This buffer must not overlap
    /// <paramref name="inverse"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the inverse values are
    /// cleared.
    /// </returns>
    public bool TryInvert(Span<double> inverse, Span<double> workspace) {
        return TryInvert(inverse, workspace, out _);
    }

    /// <summary>
    /// Computes the inverse and reports the numerical termination status.
    /// </summary>
    /// <param name="inverse">Receives <see cref="Order"/> squared row-major values.</param>
    /// <param name="workspace">At least <see cref="Order"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryInvert(
        Span<double> inverse,
        Span<double> workspace,
        out LinearAlgebraOperationReport report) {
        var elementCount = Order * Order;
        if (inverse.Length < elementCount) {
            throw new ArgumentException(
                "The inverse buffer does not contain enough values.",
                nameof(inverse));
        }

        if (workspace.Length < Order) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(workspace));
        }

        if (inverse.Overlaps(workspace)) {
            throw new ArgumentException(
                "The workspace must not overlap the inverse buffer.",
                nameof(workspace));
        }

        inverse[..elementCount].Clear();
        if (IsSingular) {
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.LuInvert,
                LinearAlgebraStatus.Singular);
            return false;
        }

        for (var column = 0; column < Order; column++) {
            workspace[..Order].Clear();
            workspace[column] = 1;
            var inverseColumn = inverse.Slice(column * Order, Order);
            if (!TrySolve(workspace, inverseColumn, out var solveReport)) {
                inverse[..elementCount].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.LuInvert,
                    solveReport.Status);
                return false;
            }
        }

        for (var row = 0; row < Order; row++) {
            for (var column = row + 1; column < Order; column++) {
                var first = (row * Order) + column;
                var second = (column * Order) + row;
                (inverse[first], inverse[second]) =
                    (inverse[second], inverse[first]);
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.LuInvert,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Computes the inverse by solving against each identity basis vector.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The factorization is singular or the inverse is non-finite.
    /// </exception>
    public double[] Invert() {
        var inverse = new double[Order * Order];
        var workspace = new double[Order];
        if (!TryInvert(inverse, workspace)) {
            throw new ArithmeticException(
                "The system is singular or its inverse is non-finite.");
        }

        return inverse;
    }

    private void Factor() {
        for (var pivotColumn = 0; pivotColumn < Order; pivotColumn++) {
            var pivotRow = pivotColumn;
            var pivotMagnitude = Math.Abs(_lu[(pivotColumn * Order) + pivotColumn]);
            for (var candidateRow = pivotColumn + 1;
                candidateRow < Order;
                candidateRow++) {
                var candidateMagnitude = Math.Abs(
                    _lu[(candidateRow * Order) + pivotColumn]);
                if (candidateMagnitude > pivotMagnitude) {
                    pivotMagnitude = candidateMagnitude;
                    pivotRow = candidateRow;
                }
            }

            if (!double.IsFinite(pivotMagnitude)) {
                throw new ArithmeticException(
                    "LU factorization produced a non-finite pivot.");
            }

            if (pivotMagnitude == 0) {
                IsSingular = true;
                return;
            }

            if (pivotRow != pivotColumn) {
                SwapRows(pivotRow, pivotColumn);
                (_permutation[pivotRow], _permutation[pivotColumn]) =
                    (_permutation[pivotColumn], _permutation[pivotRow]);
                PermutationSign = -PermutationSign;
            }

            var pivotOffset = pivotColumn * Order;
            var pivot = _lu[pivotOffset + pivotColumn];
            for (var row = pivotColumn + 1; row < Order; row++) {
                var rowOffset = row * Order;
                var factor = _lu[rowOffset + pivotColumn] / pivot;
                _lu[rowOffset + pivotColumn] = factor;
                for (var column = pivotColumn + 1; column < Order; column++) {
                    _lu[rowOffset + column] -=
                        factor * _lu[pivotOffset + column];
                }
            }
        }
    }

    private void SwapRows(int firstRow, int secondRow) {
        var firstOffset = firstRow * Order;
        var secondOffset = secondRow * Order;
        for (var column = 0; column < Order; column++) {
            (_lu[firstOffset + column], _lu[secondOffset + column]) =
                (_lu[secondOffset + column], _lu[firstOffset + column]);
        }
    }

    private void ValidateFactor() {
        for (var index = 0; index < _lu.Length; index++) {
            if (!double.IsFinite(_lu[index])) {
                throw new ArithmeticException(
                    "LU factorization produced a non-finite value.");
            }
        }
    }

    private double ComputeDeterminant() {
        var determinant = (double)PermutationSign;
        for (var index = 0; index < Order; index++) {
            determinant *= _lu[(index * Order) + index];
        }

        return determinant;
    }
}
