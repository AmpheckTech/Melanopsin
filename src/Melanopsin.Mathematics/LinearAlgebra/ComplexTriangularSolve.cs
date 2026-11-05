namespace Melanopsin.Mathematics;

/// <summary>
/// Solves complex triangular systems stored in row-major order.
/// </summary>
/// <remarks>
/// The unused triangle is ignored. A zero diagonal marks the system as
/// singular. No absolute near-singularity threshold is applied. This type
/// solves the triangular system as written and does not conjugate entries.
/// </remarks>
public static class ComplexTriangularSolve {
    /// <summary>
    /// Attempts to solve <c>A x = b</c> for a complex triangular matrix.
    /// </summary>
    /// <param name="matrix">At least <paramref name="order"/> squared values.</param>
    /// <param name="order">The number of rows and columns.</param>
    /// <param name="triangle">The populated triangle.</param>
    /// <param name="unitDiagonal">
    /// Whether the diagonal is implicitly one and ignored.
    /// </param>
    /// <param name="rightHandSide">At least <paramref name="order"/> values.</param>
    /// <param name="solution">
    /// Receives at least <paramref name="order"/> values and must not overlap the
    /// right-hand side.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the first
    /// <paramref name="order"/> values are cleared.
    /// </returns>
    public static bool TrySolve(
        ReadOnlySpan<Complex> matrix,
        int order,
        MatrixTriangle triangle,
        bool unitDiagonal,
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        return TrySolve(
            matrix,
            order,
            triangle,
            unitDiagonal,
            rightHandSide,
            solution,
            out _);
    }

    /// <summary>
    /// Attempts to solve a complex triangular system and reports the numerical
    /// termination status.
    /// </summary>
    /// <param name="matrix">At least <paramref name="order"/> squared values.</param>
    /// <param name="order">The number of rows and columns.</param>
    /// <param name="triangle">The populated triangle.</param>
    /// <param name="unitDiagonal">Whether the diagonal is implicitly one.</param>
    /// <param name="rightHandSide">At least <paramref name="order"/> values.</param>
    /// <param name="solution">Receives at least <paramref name="order"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public static bool TrySolve(
        ReadOnlySpan<Complex> matrix,
        int order,
        MatrixTriangle triangle,
        bool unitDiagonal,
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        out LinearAlgebraOperationReport report) {
        ValidateArguments(
            matrix,
            order,
            triangle,
            rightHandSide,
            solution);

        for (var index = 0; index < order; index++) {
            if (!rightHandSide[index].IsFinite) {
                solution[..order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexTriangularSolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }
        }

        if (triangle == MatrixTriangle.Lower) {
            for (var row = 0; row < order; row++) {
                var value = rightHandSide[row];
                var rowOffset = row * order;
                for (var column = 0; column < row; column++) {
                    value -= matrix[rowOffset + column] * solution[column];
                }

                if (!unitDiagonal) {
                    var diagonal = matrix[rowOffset + row];
                    if (diagonal == Complex.Zero) {
                        solution[..order].Clear();
                        report = new LinearAlgebraOperationReport(
                            LinearAlgebraOperation.ComplexTriangularSolve,
                            LinearAlgebraStatus.Singular);
                        return false;
                    }

                    value /= diagonal;
                }

                solution[row] = value;
            }
        } else {
            for (var row = order - 1; row >= 0; row--) {
                var value = rightHandSide[row];
                var rowOffset = row * order;
                for (var column = row + 1; column < order; column++) {
                    value -= matrix[rowOffset + column] * solution[column];
                }

                if (!unitDiagonal) {
                    var diagonal = matrix[rowOffset + row];
                    if (diagonal == Complex.Zero) {
                        solution[..order].Clear();
                        report = new LinearAlgebraOperationReport(
                            LinearAlgebraOperation.ComplexTriangularSolve,
                            LinearAlgebraStatus.Singular);
                        return false;
                    }

                    value /= diagonal;
                }

                solution[row] = value;
            }
        }

        for (var index = 0; index < order; index++) {
            if (!solution[index].IsFinite) {
                solution[..order].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexTriangularSolve,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexTriangularSolve,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves <c>A x = b</c> for a complex triangular matrix.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The matrix is singular or the solution is non-finite.
    /// </exception>
    public static Complex[] Solve(
        ReadOnlySpan<Complex> matrix,
        int order,
        MatrixTriangle triangle,
        bool unitDiagonal,
        ReadOnlySpan<Complex> rightHandSide) {
        var solution = new Complex[order];
        if (!TrySolve(
            matrix,
            order,
            triangle,
            unitDiagonal,
            rightHandSide,
            solution)) {
            throw new ArithmeticException(
                "The triangular system is singular or its solution is non-finite.");
        }

        return solution;
    }

    private static void ValidateArguments(
        ReadOnlySpan<Complex> matrix,
        int order,
        MatrixTriangle triangle,
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        if (order <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                "The matrix order must be positive.");
        }

        if (triangle is not MatrixTriangle.Lower and
            not MatrixTriangle.Upper) {
            throw new ArgumentOutOfRangeException(nameof(triangle));
        }

        var elementCount = checked(order * order);
        if (matrix.Length < elementCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!matrix[index].IsFinite) {
                throw new ArgumentException(
                    "The matrix must contain only finite values.",
                    nameof(matrix));
            }
        }

        if (rightHandSide.Length < order) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (solution.Length < order) {
            throw new ArgumentException(
                "The solution buffer does not contain enough values.",
                nameof(solution));
        }

        if (rightHandSide.Overlaps(solution)) {
            throw new ArgumentException(
                "The solution buffer must not overlap the right-hand side.",
                nameof(solution));
        }
    }
}
