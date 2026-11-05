// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Householder QR factorization of a complex row-major matrix with at least
/// as many rows as columns.
/// </summary>
/// <remarks>
/// The factorization computes <c>A = Q R</c>, where <c>Q</c> has orthonormal
/// columns under the Hermitian inner product and <c>R</c> is upper
/// triangular. Rank deficiency is reported only when an exactly zero diagonal
/// entry occurs. No absolute near-zero threshold is applied; callers that need
/// numerical rank should use singular values or an explicit relative
/// tolerance. Householder QR follows Golub and Van Loan, <i>Matrix
/// Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class ComplexQrDecomposition {
    private readonly Complex[] _qr;
    private readonly Complex[] _diagonal;
    private readonly double[] _reflectorScale;

    /// <summary>
    /// Factors a complex row-major matrix.
    /// </summary>
    /// <param name="source">At least <c>rows * columns</c> values.</param>
    /// <param name="rows">The number of matrix rows.</param>
    /// <param name="columns">The number of matrix columns.</param>
    /// <exception cref="ArgumentException">
    /// The dimensions are invalid, the source is too short, or the source is
    /// not finite.
    /// </exception>
    public ComplexQrDecomposition(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        if (rows <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(rows),
                rows,
                "The row count must be positive.");
        }

        if (columns <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                columns,
                "The column count must be positive.");
        }

        if (rows < columns) {
            throw new ArgumentException(
                "Householder QR requires at least as many rows as columns.",
                nameof(rows));
        }

        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        Rows = rows;
        Columns = columns;
        _qr = new Complex[elementCount];
        _diagonal = new Complex[columns];
        _reflectorScale = new double[columns];
        for (var index = 0; index < elementCount; index++) {
            var value = source[index];
            if (!value.IsFinite) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }

            _qr[index] = value;
        }

        Factor();
    }

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>
    /// Gets whether every diagonal entry of R is nonzero.
    /// </summary>
    public bool IsFullColumnRank {
        get {
            for (var index = 0; index < Columns; index++) {
                if (_diagonal[index] == Complex.Zero) {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Solves a square system using the existing factorization.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">
    /// Receives at least <see cref="Rows"/> values and must not overlap the
    /// right-hand side.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the first
    /// <see cref="Rows"/> values are cleared.
    /// </returns>
    public bool TrySolve(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        return TrySolve(rightHandSide, solution, out _);
    }

    /// <summary>
    /// Solves a square complex system and reports the numerical termination
    /// status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Rows"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TrySolve(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        out LinearAlgebraOperationReport report) {
        if (Rows != Columns) {
            throw new InvalidOperationException(
                "TrySolve requires a square matrix; use TryLeastSquares otherwise.");
        }

        ValidateSolveArguments(rightHandSide, solution, Rows);
        if (!IsFullColumnRank) {
            solution[..Rows].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexQrSolve,
                LinearAlgebraStatus.Singular);
            return false;
        }

        for (var index = 0; index < Rows; index++) {
            if (!rightHandSide[index].IsFinite) {
                solution[..Rows].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexQrSolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            solution[index] = rightHandSide[index];
        }

        ApplyConjugateTransposeTo(solution[..Rows]);
        if (!SolveUpper(solution[..Columns], solution)) {
            solution[..Rows].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexQrSolve,
                LinearAlgebraStatus.NonFiniteOutput);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexQrSolve,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves a square system using the existing factorization.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The matrix is not square.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The matrix is rank deficient or the solution is non-finite.
    /// </exception>
    public Complex[] Solve(ReadOnlySpan<Complex> rightHandSide) {
        var solution = new Complex[Rows];
        if (!TrySolve(rightHandSide, solution)) {
            throw new ArithmeticException(
                "The matrix is rank deficient or its solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Solves a full-column-rank least-squares problem using the Hermitian
    /// norm.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">
    /// Receives at least <see cref="Columns"/> values and must not overlap
    /// either other buffer.
    /// </param>
    /// <param name="workspace">
    /// At least <see cref="Rows"/> values and must not overlap either other
    /// buffer.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; otherwise the first
    /// <see cref="Columns"/> values are cleared.
    /// </returns>
    public bool TryLeastSquares(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        Span<Complex> workspace) {
        return TryLeastSquares(rightHandSide, solution, workspace, out _);
    }

    /// <summary>
    /// Solves a full-column-rank complex least-squares problem and reports the
    /// numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Columns"/> values.</param>
    /// <param name="workspace">At least <see cref="Rows"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryLeastSquares(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        Span<Complex> workspace,
        out LinearAlgebraOperationReport report) {
        if (workspace.Length < Rows) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(workspace));
        }

        if (solution.Length < Columns) {
            throw new ArgumentException(
                "The solution buffer does not contain enough values.",
                nameof(solution));
        }

        if (rightHandSide.Length < Rows) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (rightHandSide.Overlaps(solution) ||
            rightHandSide.Overlaps(workspace) ||
            solution.Overlaps(workspace)) {
            throw new ArgumentException(
                "The solution and workspace must not overlap the input buffers.");
        }

        if (!IsFullColumnRank) {
            solution[..Columns].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexQrLeastSquares,
                LinearAlgebraStatus.RankDeficient);
            return false;
        }

        for (var index = 0; index < Rows; index++) {
            var value = rightHandSide[index];
            if (!value.IsFinite) {
                solution[..Columns].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexQrLeastSquares,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            workspace[index] = value;
        }

        ApplyConjugateTransposeTo(workspace[..Rows]);
        if (!SolveUpper(workspace[..Columns], solution[..Columns])) {
            solution[..Columns].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexQrLeastSquares,
                LinearAlgebraStatus.NonFiniteOutput);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexQrLeastSquares,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves a full-column-rank least-squares problem using the Hermitian
    /// norm.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The matrix is rank deficient or the solution is non-finite.
    /// </exception>
    public Complex[] LeastSquares(ReadOnlySpan<Complex> rightHandSide) {
        var solution = new Complex[Columns];
        var workspace = new Complex[Rows];
        if (!TryLeastSquares(rightHandSide, solution, workspace)) {
            throw new ArithmeticException(
                "The matrix is rank deficient or its solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Gets the economy Q factor as a row-major
    /// <see cref="Rows"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public Complex[] GetQ() {
        var q = new Complex[checked(Rows * Columns)];
        if (!TryGetQ(q)) {
            throw new InvalidOperationException(
                "The Q buffer cannot be represented.");
        }

        return q;
    }

    /// <summary>
    /// Writes the economy Q factor as a row-major
    /// <see cref="Rows"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public bool TryGetQ(Span<Complex> destination) {
        return TryGetQ(destination, out _);
    }

    /// <summary>
    /// Writes the economy Q factor and reports a non-finite result.
    /// </summary>
    /// <param name="destination">Receives the row-major Q factor.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryGetQ(
        Span<Complex> destination,
        out LinearAlgebraOperationReport report) {
        var elementCount = checked(Rows * Columns);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var column = 0; column < Columns; column++) {
            destination[(column * Columns) + column] = Complex.One;
        }

        for (var reflector = Columns - 1; reflector >= 0; reflector--) {
            ApplyReflectorToQ(destination[..elementCount], reflector);
        }

        for (var index = 0; index < elementCount; index++) {
            if (!destination[index].IsFinite) {
                destination[..elementCount].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexQrFactorExtraction,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexQrFactorExtraction,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Gets the upper-triangular R factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public Complex[] GetR() {
        var r = new Complex[checked(Columns * Columns)];
        CopyRTo(r);
        return r;
    }

    /// <summary>
    /// Writes the upper-triangular R factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public void CopyRTo(Span<Complex> destination) {
        var elementCount = checked(Columns * Columns);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var row = 0; row < Columns; row++) {
            destination[(row * Columns) + row] = _diagonal[row];
            for (var column = row + 1; column < Columns; column++) {
                destination[(row * Columns) + column] =
                    _qr[(row * Columns) + column];
            }
        }
    }

    private void Factor() {
        for (var pivotColumn = 0;
            pivotColumn < Columns;
            pivotColumn++) {
            var columnNorm = ColumnNorm(pivotColumn, pivotColumn);
            if (columnNorm == 0) {
                _diagonal[pivotColumn] = Complex.Zero;
                _reflectorScale[pivotColumn] = 0;
                continue;
            }

            var diagonalOffset = (pivotColumn * Columns) + pivotColumn;
            var current = _qr[diagonalOffset];
            var phase = current == Complex.Zero
                ? Complex.One
                : current / new Complex(current.Magnitude, 0);
            var tau = -(phase * columnNorm);
            var firstReflectorValue = current - tau;
            _qr[diagonalOffset] = firstReflectorValue;
            _diagonal[pivotColumn] = tau;

            var normSquared = firstReflectorValue.MagnitudeSquared;
            for (var row = pivotColumn + 1; row < Rows; row++) {
                normSquared += _qr[(row * Columns) + pivotColumn]
                    .MagnitudeSquared;
            }

            if (!double.IsFinite(normSquared) || normSquared == 0) {
                throw new ArithmeticException(
                    "QR factorization produced an invalid Householder vector.");
            }

            _reflectorScale[pivotColumn] = 2 / normSquared;
            for (var column = pivotColumn + 1;
                column < Columns;
                column++) {
                var dot = firstReflectorValue.Conjugate() *
                    _qr[(pivotColumn * Columns) + column];
                for (var row = pivotColumn + 1; row < Rows; row++) {
                    dot += _qr[(row * Columns) + pivotColumn].Conjugate() *
                        _qr[(row * Columns) + column];
                }

                var scale = _reflectorScale[pivotColumn];
                var adjustment = dot * scale;
                _qr[(pivotColumn * Columns) + column] -=
                    adjustment * firstReflectorValue;
                for (var row = pivotColumn + 1; row < Rows; row++) {
                    _qr[(row * Columns) + column] -=
                        adjustment * _qr[(row * Columns) + pivotColumn];
                }
            }
        }

        for (var index = 0; index < _qr.Length; index++) {
            if (!_qr[index].IsFinite) {
                throw new ArithmeticException(
                    "QR factorization produced a non-finite value.");
            }
        }
    }

    private double ColumnNorm(int firstRow, int column) {
        var scale = 0.0;
        var sumSquares = 1.0;
        for (var row = firstRow; row < Rows; row++) {
            var absolute = _qr[(row * Columns) + column].Magnitude;
            if (absolute == 0) {
                continue;
            }

            if (scale < absolute) {
                var ratio = scale / absolute;
                sumSquares = 1 + (sumSquares * ratio * ratio);
                scale = absolute;
            } else {
                var ratio = absolute / scale;
                sumSquares += ratio * ratio;
            }
        }

        return scale == 0 ? 0 : scale * Math.Sqrt(sumSquares);
    }

    private void ApplyConjugateTransposeTo(Span<Complex> vector) {
        for (var reflector = 0; reflector < Columns; reflector++) {
            var scale = _reflectorScale[reflector];
            if (scale == 0) {
                continue;
            }

            var firstRow = reflector;
            var firstReflectorValue = _qr[(firstRow * Columns) + reflector];
            var dot = firstReflectorValue.Conjugate() * vector[firstRow];
            for (var row = firstRow + 1; row < Rows; row++) {
                dot += _qr[(row * Columns) + reflector].Conjugate() *
                    vector[row];
            }

            var adjustment = dot * scale;
            vector[firstRow] -= adjustment * firstReflectorValue;
            for (var row = firstRow + 1; row < Rows; row++) {
                vector[row] -= adjustment *
                    _qr[(row * Columns) + reflector];
            }
        }
    }

    private bool SolveUpper(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        for (var row = Columns - 1; row >= 0; row--) {
            var value = rightHandSide[row];
            var rowOffset = row * Columns;
            for (var column = row + 1; column < Columns; column++) {
                value -= _qr[rowOffset + column] * solution[column];
            }

            var diagonal = _diagonal[row];
            if (diagonal == Complex.Zero) {
                return false;
            }

            value /= diagonal;
            if (!value.IsFinite) {
                return false;
            }

            solution[row] = value;
        }

        return true;
    }

    private void ApplyReflectorToQ(Span<Complex> q, int reflector) {
        var scale = _reflectorScale[reflector];
        if (scale == 0) {
            return;
        }

        var firstReflectorValue = _qr[(reflector * Columns) + reflector];
        for (var column = 0; column < Columns; column++) {
            var dot = firstReflectorValue.Conjugate() *
                q[(reflector * Columns) + column];
            for (var row = reflector + 1; row < Rows; row++) {
                dot += _qr[(row * Columns) + reflector].Conjugate() *
                    q[(row * Columns) + column];
            }

            var adjustment = dot * scale;
            q[(reflector * Columns) + column] -=
                adjustment * firstReflectorValue;
            for (var row = reflector + 1; row < Rows; row++) {
                q[(row * Columns) + column] -=
                    adjustment * _qr[(row * Columns) + reflector];
            }
        }
    }

    private static void ValidateSolveArguments(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        int order) {
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
