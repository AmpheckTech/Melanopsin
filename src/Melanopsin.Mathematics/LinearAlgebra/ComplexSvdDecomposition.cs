// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A singular value decomposition of a complex row-major matrix.
/// </summary>
/// <remarks>
/// The decomposition computes <c>A = U diag(S) V^H</c> using a one-sided
/// Jacobi method with phase normalization. <c>U</c> is rows-by-rank, <c>V</c>
/// is columns-by-rank, and rank is <c>min(rows, columns)</c>. Singular values
/// are sorted in descending order. APIs that truncate components use a
/// tolerance relative to the largest singular value. The one-sided Jacobi
/// formulation follows Hestenes (1958),
/// <see href="https://doi.org/10.1137/0106005">doi:10.1137/0106005</see>;
/// complex phase normalization follows the treatment in Golub and Van Loan,
/// <i>Matrix Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class ComplexSvdDecomposition {
    private const int MaximumSweeps = 100;
    private readonly Complex[] _u;
    private readonly Complex[] _v;
    private readonly double[] _singularValues;

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
    public ComplexSvdDecomposition(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        DenseMatrixValidation.Validate(rows, columns);
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!source[index].IsFinite) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }
        }

        Rows = rows;
        Columns = columns;
        ComponentCount = Math.Min(rows, columns);
        _singularValues = new double[ComponentCount];

        if (rows >= columns) {
            _u = new Complex[checked(rows * columns)];
            _v = new Complex[checked(columns * columns)];
            FactorTall(source[..elementCount], rows, columns, _u, _v);
        } else {
            var transposed = new Complex[elementCount];
            DenseMatrix.ConjugateTranspose(
                source[..elementCount],
                rows,
                columns,
                transposed);
            var transposedU = new Complex[elementCount];
            var transposedV = new Complex[checked(rows * rows)];
            FactorTall(transposed, columns, rows, transposedU, transposedV);
            _u = transposedV;
            _v = transposedU;
        }
    }

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>
    /// Gets the number of singular values, equal to
    /// <c>min(rows, columns)</c>.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the singular values in descending order.
    /// </summary>
    public ReadOnlySpan<double> SingularValues => _singularValues;

    public double LargestSingularValue =>
        _singularValues.Length == 0 ? 0 : _singularValues[0];

    public double SmallestSingularValue =>
        _singularValues.Length == 0 ? 0 : _singularValues[^1];

    /// <summary>
    /// Gets the spectral condition number. A zero smallest singular value
    /// produces positive infinity.
    /// </summary>
    public double ConditionNumber {
        get {
            var smallest = SmallestSingularValue;
            return smallest == 0
                ? double.PositiveInfinity
                : LargestSingularValue / smallest;
        }
    }

    /// <summary>
    /// Gets the exact count of nonzero singular values.
    /// </summary>
    public int Rank => CountRank(0);

    /// <summary>
    /// Counts singular values strictly greater than the largest singular value
    /// times <paramref name="relativeTolerance"/>.
    /// </summary>
    public int CountRank(double relativeTolerance) {
        ValidateRelativeTolerance(relativeTolerance);
        var threshold = LargestSingularValue * relativeTolerance;
        var rank = 0;
        for (var index = 0; index < _singularValues.Length; index++) {
            if (_singularValues[index] > threshold) {
                rank++;
            }
        }

        return rank;
    }

    /// <summary>
    /// Copies the economy U factor as a row-major
    /// <see cref="Rows"/>-by-<see cref="ComponentCount"/> matrix.
    /// </summary>
    public void CopyUTo(Span<Complex> destination) {
        CopyBuffer(_u, destination, nameof(destination));
    }

    /// <summary>
    /// Gets the economy U factor as a row-major
    /// <see cref="Rows"/>-by-<see cref="ComponentCount"/> matrix.
    /// </summary>
    public Complex[] GetU() {
        return _u.AsSpan().ToArray();
    }

    /// <summary>
    /// Copies the economy V factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="ComponentCount"/> matrix.
    /// </summary>
    public void CopyVTo(Span<Complex> destination) {
        CopyBuffer(_v, destination, nameof(destination));
    }

    /// <summary>
    /// Gets the economy V factor as a row-major
    /// <see cref="Columns"/>-by-<see cref="ComponentCount"/> matrix.
    /// </summary>
    public Complex[] GetV() {
        return _v.AsSpan().ToArray();
    }

    /// <summary>
    /// Copies <c>V^H</c> as a row-major
    /// <see cref="ComponentCount"/>-by-<see cref="Columns"/> matrix.
    /// </summary>
    public void CopyConjugateTransposeVTo(Span<Complex> destination) {
        var elementCount = checked(ComponentCount * Columns);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        for (var row = 0; row < Columns; row++) {
            for (var column = 0; column < ComponentCount; column++) {
                destination[(column * Columns) + row] =
                    _v[(row * ComponentCount) + column].Conjugate();
            }
        }
    }

    /// <summary>
    /// Solves a square system.
    /// </summary>
    public bool TrySolve(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution) {
        return TrySolve(rightHandSide, solution, out _);
    }

    /// <summary>
    /// Solves a square system and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Columns"/> values.</param>
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

        return TryLeastSquaresCore(
            rightHandSide,
            solution,
            0,
            requireFullRank: true,
            LinearAlgebraOperation.ComplexSvdSolve,
            out report);
    }

    /// <summary>
    /// Solves a square system.
    /// </summary>
    public Complex[] Solve(ReadOnlySpan<Complex> rightHandSide) {
        var solution = new Complex[Columns];
        if (!TrySolve(rightHandSide, solution)) {
            throw new ArithmeticException(
                "The matrix is singular or the solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Computes a least-squares or minimum-norm least-squares solution under
    /// the Hermitian norm.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Columns"/> values.</param>
    /// <param name="relativeTolerance">
    /// Singular values at or below this fraction of the largest singular value
    /// are treated as zero for this solve. Zero uses exact nonzero values.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all arithmetic is finite; otherwise the
    /// solution is cleared.
    /// </returns>
    public bool TryLeastSquares(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        double relativeTolerance = 0) {
        return TryLeastSquares(rightHandSide, solution, out _, relativeTolerance);
    }

    /// <summary>
    /// Computes a least-squares or minimum-norm least-squares solution under
    /// the Hermitian norm and reports the numerical termination status.
    /// </summary>
    /// <param name="rightHandSide">At least <see cref="Rows"/> values.</param>
    /// <param name="solution">Receives at least <see cref="Columns"/> values.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <param name="relativeTolerance">
    /// Singular values at or below this fraction of the largest singular value
    /// are treated as zero for this solve. Zero uses exact nonzero values.
    /// </param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryLeastSquares(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        out LinearAlgebraOperationReport report,
        double relativeTolerance = 0) {
        return TryLeastSquaresCore(
            rightHandSide,
            solution,
            relativeTolerance,
            requireFullRank: false,
            LinearAlgebraOperation.ComplexSvdLeastSquares,
            out report);
    }

    /// <summary>
    /// Computes a least-squares or minimum-norm least-squares solution under
    /// the Hermitian norm.
    /// </summary>
    public Complex[] LeastSquares(
        ReadOnlySpan<Complex> rightHandSide,
        double relativeTolerance = 0) {
        var solution = new Complex[Columns];
        if (!TryLeastSquares(
            rightHandSide,
            solution,
            relativeTolerance)) {
            throw new ArithmeticException(
                "The least-squares solution is non-finite.");
        }

        return solution;
    }

    /// <summary>
    /// Computes the Moore-Penrose pseudoinverse as a row-major
    /// <see cref="Columns"/>-by-<see cref="Rows"/> matrix.
    /// </summary>
    public Complex[] Pseudoinverse(double relativeTolerance = 0) {
        var result = new Complex[checked(Columns * Rows)];
        if (!TryPseudoinverse(result, relativeTolerance)) {
            throw new ArithmeticException(
                "The pseudoinverse is non-finite.");
        }

        return result;
    }

    /// <summary>
    /// Writes the Moore-Penrose pseudoinverse as a row-major
    /// <see cref="Columns"/>-by-<see cref="Rows"/> matrix.
    /// </summary>
    public bool TryPseudoinverse(
        Span<Complex> destination,
        double relativeTolerance = 0) {
        return TryPseudoinverse(destination, out _, relativeTolerance);
    }

    /// <summary>
    /// Writes the Moore-Penrose pseudoinverse and reports the numerical
    /// termination status.
    /// </summary>
    /// <param name="destination">Receives the row-major pseudoinverse.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <param name="relativeTolerance">
    /// Singular values at or below this fraction of the largest singular value
    /// are treated as zero.
    /// </param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryPseudoinverse(
        Span<Complex> destination,
        out LinearAlgebraOperationReport report,
        double relativeTolerance = 0) {
        ValidateRelativeTolerance(relativeTolerance);
        var elementCount = checked(Columns * Rows);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        var threshold = LargestSingularValue * relativeTolerance;
        for (var component = 0; component < ComponentCount; component++) {
            var singularValue = _singularValues[component];
            if (singularValue <= threshold) {
                continue;
            }

            var inverseSingularValue = 1 / singularValue;
            for (var row = 0; row < Columns; row++) {
                var v = _v[(row * ComponentCount) + component];
                if (v == Complex.Zero) {
                    continue;
                }

                var scaled = v * inverseSingularValue;
                for (var column = 0; column < Rows; column++) {
                    destination[(row * Rows) + column] += scaled *
                        _u[(column * ComponentCount) + component].Conjugate();
                }
            }
        }

        for (var index = 0; index < elementCount; index++) {
            if (!destination[index].IsFinite) {
                destination[..elementCount].Clear();
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ComplexSvdPseudoinverse,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexSvdPseudoinverse,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Computes the inverse of a nonsingular square matrix through its SVD and
    /// reports the numerical termination status.
    /// </summary>
    /// <param name="destination">Receives the row-major inverse.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <param name="relativeTolerance">
    /// A non-negative relative singular-value threshold. Zero requires every
    /// singular value to be exactly nonzero.
    /// </param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryInvert(
        Span<Complex> destination,
        out LinearAlgebraOperationReport report,
        double relativeTolerance = 0) {
        if (Rows != Columns) {
            throw new InvalidOperationException("Invert requires a square matrix.");
        }

        ValidateRelativeTolerance(relativeTolerance);
        var threshold = LargestSingularValue * relativeTolerance;
        if (!IsFullRank(threshold)) {
            var elementCount = checked(Columns * Columns);
            if (destination.Length < elementCount) {
                throw new ArgumentException(
                    "The destination does not contain enough values.",
                    nameof(destination));
            }

            destination[..elementCount].Clear();
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexSvdInvert,
                LinearAlgebraStatus.Singular);
            return false;
        }

        if (!TryPseudoinverse(destination, out var pseudoinverseReport, relativeTolerance)) {
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ComplexSvdInvert,
                pseudoinverseReport.Status);
            return false;
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.ComplexSvdInvert,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Computes the inverse of a nonsingular square matrix through its SVD.
    /// </summary>
    /// <param name="destination">Receives the row-major inverse.</param>
    /// <param name="relativeTolerance">A non-negative relative singular-value threshold.</param>
    /// <returns><see langword="true"/> on success; otherwise the destination is cleared.</returns>
    public bool TryInvert(Span<Complex> destination, double relativeTolerance = 0) {
        return TryInvert(destination, out _, relativeTolerance);
    }

    /// <summary>
    /// Computes the inverse of a nonsingular square matrix through its SVD.
    /// </summary>
    public Complex[] Invert() {
        if (Rows != Columns) {
            throw new InvalidOperationException(
                "Invert requires a square matrix.");
        }

        var result = new Complex[checked(Rows * Columns)];
        if (!TryInvert(result)) {
            throw new ArithmeticException(
                "The matrix is singular or its inverse is non-finite.");
        }

        return result;
    }

    private bool TryLeastSquaresCore(
        ReadOnlySpan<Complex> rightHandSide,
        Span<Complex> solution,
        double relativeTolerance,
        bool requireFullRank,
        LinearAlgebraOperation operation,
        out LinearAlgebraOperationReport report) {
        ValidateRelativeTolerance(relativeTolerance);
        if (rightHandSide.Length < Rows) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (solution.Length < Columns) {
            throw new ArgumentException(
                "The solution buffer does not contain enough values.",
                nameof(solution));
        }

        if (rightHandSide.Overlaps(solution)) {
            throw new ArgumentException(
                "The solution buffer must not overlap the right-hand side.",
                nameof(solution));
        }

        for (var index = 0; index < Rows; index++) {
            if (!rightHandSide[index].IsFinite) {
                solution[..Columns].Clear();
                report = new LinearAlgebraOperationReport(
                    operation,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }
        }

        var threshold = LargestSingularValue * relativeTolerance;
        if (requireFullRank && !IsFullRank(threshold)) {
            solution[..Columns].Clear();
            report = new LinearAlgebraOperationReport(
                operation,
                LinearAlgebraStatus.Singular);
            return false;
        }

        solution[..Columns].Clear();
        for (var component = 0; component < ComponentCount; component++) {
            var singularValue = _singularValues[component];
            if (singularValue <= threshold) {
                continue;
            }

            var projection = Complex.Zero;
            for (var row = 0; row < Rows; row++) {
                projection += _u[(row * ComponentCount) + component].Conjugate() *
                    rightHandSide[row];
            }

            var coefficient = projection / singularValue;
            for (var column = 0; column < Columns; column++) {
                solution[column] += coefficient *
                    _v[(column * ComponentCount) + component];
            }
        }

        for (var index = 0; index < Columns; index++) {
            if (!solution[index].IsFinite) {
                solution[..Columns].Clear();
                report = new LinearAlgebraOperationReport(
                    operation,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new LinearAlgebraOperationReport(
            operation,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    private bool IsFullRank(double threshold) {
        for (var index = 0; index < ComponentCount; index++) {
            if (_singularValues[index] <= threshold) {
                return false;
            }
        }

        return true;
    }

    private void FactorTall(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns,
        Span<Complex> u,
        Span<Complex> v) {
        var elementCount = checked(rows * columns);
        var scale = 0.0;
        for (var index = 0; index < elementCount; index++) {
            scale = Math.Max(scale, source[index].Magnitude);
        }

        var work = new Complex[elementCount];
        if (scale > 0) {
            for (var index = 0; index < elementCount; index++) {
                work[index] = source[index] / scale;
            }
        }

        v[..checked(columns * columns)].Clear();
        for (var column = 0; column < columns; column++) {
            v[(column * columns) + column] = Complex.One;
        }

        var converged = false;
        for (var sweep = 0; sweep < MaximumSweeps; sweep++) {
            var changed = false;
            for (var first = 0; first < columns - 1; first++) {
                for (var second = first + 1; second < columns; second++) {
                    if (!RotateColumns(
                        work,
                        rows,
                        columns,
                        v,
                        first,
                        second)) {
                        continue;
                    }

                    changed = true;
                }
            }

            if (!changed) {
                converged = true;
                break;
            }
        }

        if (!converged) {
            throw new ArithmeticException(
                "The complex Jacobi singular value iteration did not converge.");
        }

        var componentOrder = new int[columns];
        var componentNorms = new double[columns];
        for (var component = 0; component < columns; component++) {
            componentOrder[component] = component;
            componentNorms[component] = ColumnNorm(work, rows, columns, component);
        }

        Array.Sort(componentOrder, (left, right) =>
            componentNorms[right].CompareTo(componentNorms[left]));
        var sortedWork = new Complex[elementCount];
        var sortedV = new Complex[checked(columns * columns)];
        for (var destinationComponent = 0;
            destinationComponent < columns;
            destinationComponent++) {
            var sourceComponent = componentOrder[destinationComponent];
            _singularValues[destinationComponent] =
                componentNorms[sourceComponent] * scale;
            for (var row = 0; row < rows; row++) {
                sortedWork[(row * columns) + destinationComponent] =
                    work[(row * columns) + sourceComponent];
            }

            for (var row = 0; row < columns; row++) {
                sortedV[(row * columns) + destinationComponent] =
                    v[(row * columns) + sourceComponent];
            }
        }

        for (var component = 0; component < columns; component++) {
            var singularValue = _singularValues[component];
            if (singularValue == 0) {
                continue;
            }

            var inverseScale = scale / singularValue;
            for (var row = 0; row < rows; row++) {
                u[(row * columns) + component] =
                    sortedWork[(row * columns) + component] * inverseScale;
            }
        }

        sortedV.AsSpan().CopyTo(v);
    }

    private static bool RotateColumns(
        Span<Complex> matrix,
        int rows,
        int columns,
        Span<Complex> v,
        int first,
        int second) {
        var firstSquaredNorm = 0.0;
        var secondSquaredNorm = 0.0;
        var crossProduct = Complex.Zero;
        for (var row = 0; row < rows; row++) {
            var firstValue = matrix[(row * columns) + first];
            var secondValue = matrix[(row * columns) + second];
            firstSquaredNorm += firstValue.MagnitudeSquared;
            secondSquaredNorm += secondValue.MagnitudeSquared;
            crossProduct += firstValue.Conjugate() * secondValue;
        }

        var crossMagnitude = crossProduct.Magnitude;
        if (crossMagnitude == 0) {
            return false;
        }

        var normProduct = Math.Sqrt(firstSquaredNorm * secondSquaredNorm);
        var threshold = 8 * ScalarMath.MachineEpsilon * normProduct;
        if (crossMagnitude <= threshold) {
            return false;
        }

        var phase = crossProduct.Conjugate() / crossMagnitude;
        for (var row = 0; row < rows; row++) {
            matrix[(row * columns) + second] *= phase;
        }

        for (var row = 0; row < columns; row++) {
            v[(row * columns) + second] *= phase;
        }

        var zeta = (secondSquaredNorm - firstSquaredNorm) /
            (2 * crossMagnitude);
        var t = Math.CopySign(
            1 / (Math.Abs(zeta) + FloatingPointMath.Hypot(1, zeta)),
            zeta);
        if (t == 0) {
            return false;
        }

        var cosine = 1 / Math.Sqrt(1 + (t * t));
        var sine = cosine * t;
        for (var row = 0; row < rows; row++) {
            var firstOffset = (row * columns) + first;
            var secondOffset = (row * columns) + second;
            var firstValue = matrix[firstOffset];
            var secondValue = matrix[secondOffset];
            matrix[firstOffset] = (cosine * firstValue) - (sine * secondValue);
            matrix[secondOffset] = (sine * firstValue) + (cosine * secondValue);
        }

        for (var row = 0; row < columns; row++) {
            var firstOffset = (row * columns) + first;
            var secondOffset = (row * columns) + second;
            var firstValue = v[firstOffset];
            var secondValue = v[secondOffset];
            v[firstOffset] = (cosine * firstValue) - (sine * secondValue);
            v[secondOffset] = (sine * firstValue) + (cosine * secondValue);
        }

        return true;
    }

    private static double ColumnNorm(
        ReadOnlySpan<Complex> matrix,
        int rows,
        int columns,
        int component) {
        var scale = 0.0;
        var sumSquares = 1.0;
        for (var row = 0; row < rows; row++) {
            var absolute = matrix[(row * columns) + component].Magnitude;
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

    private static void CopyBuffer(
        ReadOnlySpan<Complex> source,
        Span<Complex> destination,
        string parameterName) {
        if (destination.Length < source.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                parameterName);
        }

        source.CopyTo(destination);
    }

    private static void ValidateRelativeTolerance(double relativeTolerance) {
        if (!double.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }
    }
}
