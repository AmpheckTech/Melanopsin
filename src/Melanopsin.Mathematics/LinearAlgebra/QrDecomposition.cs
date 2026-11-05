// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A Householder QR factorization of a real row-major matrix with at least as
/// many rows as columns.
/// </summary>
/// <remarks>
/// The factorization computes <c>A = Q R</c>, where <c>Q</c> has orthonormal
/// columns and <c>R</c> is upper triangular. Rank deficiency is reported only
/// when an exactly zero diagonal entry occurs. No absolute near-zero threshold
/// is applied; callers that need numerical rank should use singular values or
/// an explicit relative tolerance. Householder QR follows Golub and Van Loan,
/// <i>Matrix Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class QrDecomposition {
    private readonly double[] _qr;
    private readonly double[] _diagonal;
    private readonly double[] _reflectorScale;

    /// <summary>
    /// Factors a real row-major matrix.
    /// </summary>
    /// <param name="source">At least <c>rows * columns</c> values.</param>
    /// <param name="rows">The number of matrix rows.</param>
    /// <param name="columns">The number of matrix columns.</param>
    /// <exception cref="ArgumentException">
    /// The dimensions are invalid, the source is too short, or the source is
    /// not finite.
    /// </exception>
    public QrDecomposition(
        ReadOnlySpan<double> source,
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
        _qr = new double[elementCount];
        _diagonal = new double[columns];
        _reflectorScale = new double[columns];
        for (var index = 0; index < elementCount; index++) {
            var value = source[index];
            if (!double.IsFinite(value)) {
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
                if (_diagonal[index] == 0) {
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
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution) {
        return TrySolve(rightHandSide, solution, out _);
    }

    /// <summary>
    /// Solves a square system and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Rows"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TrySolve(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out LinearAlgebraOperationReport report) {
        if (Rows != Columns) {
            throw new InvalidOperationException(
                "TrySolve requires a square matrix; use TryLeastSquares otherwise.");
        }

        ValidateSolveArguments(rightHandSide, solution, Rows);
        if (!IsFullColumnRank) {
            solution[..Rows].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.QrSolve,
                LinearAlgebraStatus.Singular);
            return false;
        }

        for (var index = 0; index < Rows; index++) {
            if (!double.IsFinite(rightHandSide[index])) {
                solution[..Rows].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.QrSolve,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            solution[index] = rightHandSide[index];
        }

        ApplyTransposeTo(solution[..Rows]);
        if (!SolveUpper(solution[..Columns], solution)) {
            solution[..Rows].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.QrSolve,
                LinearAlgebraStatus.NonFiniteOutput);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.QrSolve,
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
    public double[] Solve(ReadOnlySpan<double> rightHandSide) {
        var solution = new double[Rows];
        if (!TrySolve(rightHandSide, solution)) {
            throw new ArithmeticException(
                "The matrix is rank deficient or its solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Solves a full-column-rank least-squares problem.
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
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace) {
        return TryLeastSquares(rightHandSide, solution, workspace, out _);
    }

    /// <summary>
    /// Solves a full-column-rank least-squares problem and reports the
    /// numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Columns"/> values.</param>
    /// <param name="workspace">At least <see cref="Rows"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryLeastSquares(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
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
                LinearAlgebraOperation.QrLeastSquares,
                LinearAlgebraStatus.RankDeficient);
            return false;
        }

        for (var index = 0; index < Rows; index++) {
            var value = rightHandSide[index];
            if (!double.IsFinite(value)) {
                solution[..Columns].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.QrLeastSquares,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }

            workspace[index] = value;
        }

        ApplyTransposeTo(workspace[..Rows]);
        if (!SolveUpper(workspace[..Columns], solution[..Columns])) {
            solution[..Columns].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.QrLeastSquares,
                LinearAlgebraStatus.NonFiniteOutput);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.QrLeastSquares,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves a full-column-rank least-squares problem.
    /// </summary>
    /// <exception cref="ArithmeticException">
    /// The matrix is rank deficient or the solution is non-finite.
    /// </exception>
    public double[] LeastSquares(ReadOnlySpan<double> rightHandSide) {
        var solution = new double[Columns];
        var workspace = new double[Rows];
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
    public double[] GetQ() {
        var q = new double[checked(Rows * Columns)];
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
    public bool TryGetQ(Span<double> destination) {
        return TryGetQ(destination, out _);
    }

    /// <summary>
    /// Writes the economy Q factor and reports a non-finite result.
    /// </summary>
    /// <param name="destination">Receives the row-major Q factor.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryGetQ(
        Span<double> destination,
        out LinearAlgebraOperationReport report) {
        var elementCount = checked(Rows * Columns);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var column = 0; column < Columns; column++) {
            destination[(column * Columns) + column] = 1;
        }

        for (var reflector = Columns - 1; reflector >= 0; reflector--) {
            ApplyReflectorToQ(destination[..elementCount], reflector);
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(destination[index])) {
                destination[..elementCount].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.QrFactorExtraction,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.QrFactorExtraction,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Gets the upper-triangular R factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public double[] GetR() {
        var r = new double[checked(Columns * Columns)];
        CopyRTo(r);
        return r;
    }

    /// <summary>
    /// Writes the upper-triangular R factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public void CopyRTo(Span<double> destination) {
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
                _diagonal[pivotColumn] = 0;
                _reflectorScale[pivotColumn] = 0;
                continue;
            }

            var diagonalOffset = (pivotColumn * Columns) + pivotColumn;
            var current = _qr[diagonalOffset];
            var tau = current >= 0 ? -columnNorm : columnNorm;
            var firstReflectorValue = current - tau;
            _qr[diagonalOffset] = firstReflectorValue;
            _diagonal[pivotColumn] = tau;

            var normSquared = firstReflectorValue * firstReflectorValue;
            for (var row = pivotColumn + 1; row < Rows; row++) {
                var value = _qr[(row * Columns) + pivotColumn];
                normSquared += value * value;
            }

            if (!double.IsFinite(normSquared) || normSquared == 0) {
                throw new ArithmeticException(
                    "QR factorization produced an invalid Householder vector.");
            }

            _reflectorScale[pivotColumn] = 2 / normSquared;
            for (var column = pivotColumn + 1;
                column < Columns;
                column++) {
                var dot = firstReflectorValue *
                    _qr[diagonalOffset - pivotColumn + column];
                for (var row = pivotColumn + 1; row < Rows; row++) {
                    dot += _qr[(row * Columns) + pivotColumn] *
                        _qr[(row * Columns) + column];
                }

                var scale = _reflectorScale[pivotColumn] * dot;
                _qr[diagonalOffset - pivotColumn + column] -=
                    scale * firstReflectorValue;
                for (var row = pivotColumn + 1; row < Rows; row++) {
                    _qr[(row * Columns) + column] -=
                        scale * _qr[(row * Columns) + pivotColumn];
                }
            }
        }

        for (var index = 0; index < _qr.Length; index++) {
            if (!double.IsFinite(_qr[index])) {
                throw new ArithmeticException(
                    "QR factorization produced a non-finite value.");
            }
        }
    }

    private double ColumnNorm(int firstRow, int column) {
        var scale = 0.0;
        var sumSquares = 1.0;
        for (var row = firstRow; row < Rows; row++) {
            var absolute = Math.Abs(_qr[(row * Columns) + column]);
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

    private void ApplyTransposeTo(Span<double> vector) {
        for (var reflector = 0; reflector < Columns; reflector++) {
            var scale = _reflectorScale[reflector];
            if (scale == 0) {
                continue;
            }

            var firstRow = reflector;
            var firstReflectorValue = _qr[(firstRow * Columns) + reflector];
            var dot = firstReflectorValue * vector[firstRow];
            for (var row = firstRow + 1; row < Rows; row++) {
                dot += _qr[(row * Columns) + reflector] * vector[row];
            }

            var adjustment = scale * dot;
            vector[firstRow] -= adjustment * firstReflectorValue;
            for (var row = firstRow + 1; row < Rows; row++) {
                vector[row] -= adjustment * _qr[(row * Columns) + reflector];
            }
        }
    }

    private bool SolveUpper(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution) {
        for (var row = Columns - 1; row >= 0; row--) {
            var value = rightHandSide[row];
            var rowOffset = row * Columns;
            for (var column = row + 1; column < Columns; column++) {
                value -= _qr[rowOffset + column] * solution[column];
            }

            var diagonal = _diagonal[row];
            if (diagonal == 0) {
                return false;
            }

            value /= diagonal;
            if (!double.IsFinite(value)) {
                return false;
            }

            solution[row] = value;
        }

        return true;
    }

    private void ApplyReflectorToQ(Span<double> q, int reflector) {
        var scale = _reflectorScale[reflector];
        if (scale == 0) {
            return;
        }

        var firstReflectorValue = _qr[(reflector * Columns) + reflector];
        for (var column = 0; column < Columns; column++) {
            var dot = firstReflectorValue * q[(reflector * Columns) + column];
            for (var row = reflector + 1; row < Rows; row++) {
                dot += _qr[(row * Columns) + reflector] *
                    q[(row * Columns) + column];
            }

            var adjustment = scale * dot;
            q[(reflector * Columns) + column] -=
                adjustment * firstReflectorValue;
            for (var row = reflector + 1; row < Rows; row++) {
                q[(row * Columns) + column] -=
                    adjustment * _qr[(row * Columns) + reflector];
            }
        }
    }

    private static void ValidateSolveArguments(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
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
