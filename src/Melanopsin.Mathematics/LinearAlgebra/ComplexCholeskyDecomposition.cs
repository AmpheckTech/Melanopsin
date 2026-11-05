namespace Melanopsin.Mathematics;

/// <summary>
/// A Cholesky factorization of a square Hermitian positive definite complex
/// matrix. Only the lower triangle of the source matrix is read.
/// </summary>
/// <remarks>
/// The caller is responsible for supplying a Hermitian matrix. A non-real or
/// non-positive diagonal value marks the factorization as not positive
/// definite. Positive values are accepted without an absolute magnitude
/// threshold. The lower Cholesky factorization follows Golub and Van Loan,
/// <i>Matrix Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class ComplexCholeskyDecomposition {
    private readonly Complex[] _lower;

    /// <summary>
    /// Factors the lower triangle of a square row-major complex matrix.
    /// </summary>
    /// <param name="source">At least <paramref name="order"/> squared values.</param>
    /// <param name="order">The number of rows and columns.</param>
    /// <exception cref="ArgumentException">
    /// The source is too short or contains a non-finite value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> is not positive.
    /// </exception>
    public ComplexCholeskyDecomposition(
        ReadOnlySpan<Complex> source,
        int order) {
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
        _lower = new Complex[elementCount];
        for (var index = 0; index < elementCount; index++) {
            if (!source[index].IsFinite) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }
        }

        IsPositiveDefinite = true;
        Factor(source);
        Determinant = IsPositiveDefinite
            ? ComputeDeterminant()
            : 0;
    }

    public int Order { get; }

    public bool IsPositiveDefinite { get; private set; }

    /// <summary>
    /// Gets the real determinant of the factored Hermitian matrix. The value
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
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        return TrySolve(rightHandSide, solution, out _);
    }

    /// <summary>
    /// Solves A x = b and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Order"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Order"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TrySolve(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
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

        if (!IsPositiveDefinite) {
            solution[..Order].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexCholeskySolve,
                LinearAlgebraStatus.NotPositiveDefinite);
            return false;
        }

        for (var row = 0; row < Order; row++) {
            var value = rightHandSide[row];
            if (!value.IsFinite) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexCholeskySolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            var rowOffset = row * Order;
            for (var column = 0; column < row; column++) {
                value -= _lower[rowOffset + column] * solution[column];
            }

            solution[row] = value / _lower[rowOffset + row];
        }

        for (var row = Order - 1; row >= 0; row--) {
            var value = solution[row];
            for (var column = row + 1; column < Order; column++) {
                value -= _lower[(column * Order) + row].Conjugate() *
                    solution[column];
            }

            solution[row] = value / _lower[(row * Order) + row].Conjugate();
        }

        for (var index = 0; index < Order; index++) {
            if (!solution[index].IsFinite) {
                solution[..Order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexCholeskySolve,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexCholeskySolve,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves A x = b using the existing factorization.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The factorization is not positive definite or the solution is
    /// non-finite.
    /// </exception>
    public Complex[] Solve(ReadOnlySpan<Complex> rightHandSide) {
        var solution = new Complex[Order];
        if (!TrySolve(rightHandSide, solution)) {
            throw new ArithmeticException(
                "The matrix is not positive definite or the solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Writes the lower-triangular Cholesky factor as a row-major square
    /// matrix. The strict upper triangle is zero.
    /// </summary>
    public void CopyLowerTo(Span<Complex> destination) {
        var elementCount = checked(Order * Order);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        _lower.AsSpan(0, elementCount).CopyTo(destination);
    }

    /// <summary>
    /// Computes the inverse using the existing factorization.
    /// </summary>
    /// <param name="inverse">
    /// Receives <see cref="Order"/> squared row-major values.
    /// </param>
    /// <param name="workspace">
    /// At least <see cref="Order"/> values and must not overlap
    /// <paramref name="inverse"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the inverse values are
    /// cleared.
    /// </returns>
    public bool TryInvert(Span<Complex> inverse, Span<Complex> workspace) {
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
        Span<Complex> inverse,
        Span<Complex> workspace,
        out LinearAlgebraOperationReport report) {
        var elementCount = checked(Order * Order);
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
        if (!IsPositiveDefinite) {
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexCholeskyInvert,
                LinearAlgebraStatus.NotPositiveDefinite);
            return false;
        }

        for (var column = 0; column < Order; column++) {
            workspace[..Order].Clear();
            workspace[column] = Complex.One;
            var inverseColumn = inverse.Slice(column * Order, Order);
            if (!TrySolve(workspace, inverseColumn, out var solveReport)) {
                inverse[..elementCount].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexCholeskyInvert,
                    solveReport.Status);
                return false;
            }
        }

        TransposeSquareInPlace(inverse[..elementCount]);
        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexCholeskyInvert,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Computes the inverse using the existing factorization.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The matrix is not positive definite or the inverse is non-finite.
    /// </exception>
    public Complex[] Invert() {
        var inverse = new Complex[checked(Order * Order)];
        var workspace = new Complex[Order];
        if (!TryInvert(inverse, workspace)) {
            throw new ArithmeticException(
                "The matrix is not positive definite or its inverse is non-finite.");
        }

        return inverse;
    }

    private void Factor(ReadOnlySpan<Complex> source) {
        for (var row = 0; row < Order; row++) {
            for (var column = 0; column <= row; column++) {
                var value = source[(row * Order) + column];
                for (var index = 0; index < column; index++) {
                    value -= _lower[(row * Order) + index] *
                        _lower[(column * Order) + index].Conjugate();
                }

                if (!value.IsFinite) {
                    MarkNotPositiveDefinite();
                    return;
                }

                if (row == column) {
                    if (value.Imaginary != 0 || value.Real <= 0) {
                        MarkNotPositiveDefinite();
                        return;
                    }

                    _lower[(row * Order) + column] =
                        new Complex(Math.Sqrt(value.Real), 0);
                } else {
                    _lower[(row * Order) + column] =
                        value / _lower[(column * Order) + column];
                }
            }
        }
    }

    private void MarkNotPositiveDefinite() {
        IsPositiveDefinite = false;
        _lower.AsSpan().Clear();
    }

    private double ComputeDeterminant() {
        var determinant = 1.0;
        for (var index = 0; index < Order; index++) {
            var diagonal = _lower[(index * Order) + index].Real;
            determinant *= diagonal * diagonal;
        }

        return determinant;
    }

    private void TransposeSquareInPlace(Span<Complex> matrix) {
        for (var row = 0; row < Order; row++) {
            for (var column = row + 1; column < Order; column++) {
                var first = (row * Order) + column;
                var second = (column * Order) + row;
                (matrix[first], matrix[second]) =
                    (matrix[second], matrix[first]);
            }
        }
    }
}
