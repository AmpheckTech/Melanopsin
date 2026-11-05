namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable sparse matrix in compressed sparse row (CSR) storage.
/// </summary>
/// <remarks>
/// Rows are contiguous in <see cref="Values"/> and
/// <see cref="ColumnIndices"/>, which keeps matrix-vector products
/// cache-friendly. Each row's column indices are strictly increasing, so the
/// representation is canonical and contains no duplicate entries. Explicit
/// zero entries are rejected; use <see cref="FromDense"/> to construct a
/// canonical matrix from dense row-major data.
/// </remarks>
public sealed partial class SparseMatrix {
    private readonly int[] _rowOffsets;
    private readonly int[] _columnIndices;
    private readonly double[] _values;

    public SparseMatrix(
        ReadOnlySpan<int> rowOffsets,
        ReadOnlySpan<int> columnIndices,
        ReadOnlySpan<double> values,
        int rowCount,
        int columnCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        if (rowOffsets.Length != checked(rowCount + 1)) {
            throw new ArgumentException(
                "The row-offset array must contain rowCount + 1 entries.",
                nameof(rowOffsets));
        }

        if (rowOffsets[0] != 0) {
            throw new ArgumentException(
                "The first row offset must be zero.",
                nameof(rowOffsets));
        }

        if (rowOffsets[rowCount] != columnIndices.Length ||
            columnIndices.Length != values.Length) {
            throw new ArgumentException(
                "The offset, column, and value arrays are inconsistent.",
                nameof(values));
        }

        for (var row = 0; row < rowCount; row++) {
            if (rowOffsets[row] > rowOffsets[row + 1]) {
                throw new ArgumentException(
                    "Row offsets must be nondecreasing.",
                    nameof(rowOffsets));
            }

            var previousColumn = -1;
            for (var index = rowOffsets[row]; index < rowOffsets[row + 1]; index++) {
                var column = columnIndices[index];
                if (column <= previousColumn || column >= columnCount) {
                    throw new ArgumentException(
                        "Column indices must be strictly increasing and in range.",
                        nameof(columnIndices));
                }

                if (!double.IsFinite(values[index]) || values[index] == 0) {
                    throw new ArgumentException(
                        "Sparse values must be finite and nonzero.",
                        nameof(values));
                }

                previousColumn = column;
            }
        }

        RowCount = rowCount;
        ColumnCount = columnCount;
        _rowOffsets = rowOffsets[..(rowCount + 1)].ToArray();
        _columnIndices = columnIndices.ToArray();
        _values = values.ToArray();
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public int NonZeroCount => _values.Length;

    /// <summary>
    /// Gets the row offsets. Entry <c>i</c> through entry <c>i + 1 - 1</c>
    /// bounds row <c>i</c> in the column and value arrays.
    /// </summary>
    public ReadOnlySpan<int> RowOffsets => _rowOffsets;

    public ReadOnlySpan<int> ColumnIndices => _columnIndices;

    public ReadOnlySpan<double> Values => _values;

    /// <summary>
    /// Creates a canonical CSR matrix from dense row-major storage.
    /// </summary>
    public static SparseMatrix FromDense(
        ReadOnlySpan<double> source,
        int rowCount,
        int columnCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        var elementCount = checked(rowCount * columnCount);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        var nonZeroCount = 0;
        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(source[index])) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }

            if (source[index] != 0) {
                nonZeroCount++;
            }
        }

        var rowOffsets = new int[rowCount + 1];
        var columnIndices = new int[nonZeroCount];
        var values = new double[nonZeroCount];
        var target = 0;
        for (var row = 0; row < rowCount; row++) {
            rowOffsets[row] = target;
            var rowOffset = row * columnCount;
            for (var column = 0; column < columnCount; column++) {
                var value = source[rowOffset + column];
                if (value != 0) {
                    columnIndices[target] = column;
                    values[target] = value;
                    target++;
                }
            }
        }

        rowOffsets[rowCount] = target;
        return new SparseMatrix(
            rowOffsets,
            columnIndices,
            values,
            rowCount,
            columnCount);
    }

    /// <summary>
    /// Writes the matrix into dense row-major storage.
    /// </summary>
    public void CopyTo(Span<double> destination) {
        var elementCount = checked(RowCount * ColumnCount);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough matrix values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var row = 0; row < RowCount; row++) {
            var rowOffset = row * ColumnCount;
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                destination[rowOffset + _columnIndices[index]] = _values[index];
            }
        }
    }

    /// <summary>
    /// Computes <c>this * vector</c> into <paramref name="destination"/>.
    /// </summary>
    public void Multiply(
        ReadOnlySpan<double> vector,
        Span<double> destination) {
        if (vector.Length < ColumnCount) {
            throw new ArgumentException(
                "The vector is too small.",
                nameof(vector));
        }

        if (destination.Length < RowCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        if (vector.Overlaps(destination)) {
            throw new ArgumentException(
                "The vector and destination must not overlap.",
                nameof(destination));
        }

        for (var row = 0; row < RowCount; row++) {
            var sum = 0.0;
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                sum += _values[index] * vector[_columnIndices[index]];
            }

            destination[row] = sum;
        }
    }

    /// <summary>
    /// Computes <c>transpose(this) * vector</c> into
    /// <paramref name="destination"/>.
    /// </summary>
    public void MultiplyTransposed(
        ReadOnlySpan<double> vector,
        Span<double> destination) {
        if (vector.Length < RowCount) {
            throw new ArgumentException(
                "The vector is too small.",
                nameof(vector));
        }

        if (destination.Length < ColumnCount) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        if (vector.Overlaps(destination)) {
            throw new ArgumentException(
                "The vector and destination must not overlap.",
                nameof(destination));
        }

        destination[..ColumnCount].Clear();
        for (var row = 0; row < RowCount; row++) {
            var value = vector[row];
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                destination[_columnIndices[index]] += _values[index] * value;
            }
        }
    }

    public double FrobeniusNorm {
        get {
            var sum = 0.0;
            for (var index = 0; index < _values.Length; index++) {
                sum += _values[index] * _values[index];
            }

            return Math.Sqrt(sum);
        }
    }

    /// <summary>
    /// Returns the sum of diagonal entries. Returns zero for rectangular
    /// matrices when no diagonal entry exists.
    /// </summary>
    public double Trace {
        get {
            var result = 0.0;
            var diagonalCount = Math.Min(RowCount, ColumnCount);
            for (var row = 0; row < diagonalCount; row++) {
                for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                    if (_columnIndices[index] == row) {
                        result += _values[index];
                        break;
                    }

                    if (_columnIndices[index] > row) {
                        break;
                    }
                }
            }

            return result;
        }
    }

    public SparseMatrix Transpose() {
        var rowOffsets = new int[ColumnCount + 1];
        for (var index = 0; index < _columnIndices.Length; index++) {
            rowOffsets[_columnIndices[index] + 1]++;
        }

        for (var row = 0; row < ColumnCount; row++) {
            rowOffsets[row + 1] += rowOffsets[row];
        }

        var next = rowOffsets[..ColumnCount].ToArray();
        var columnIndices = new int[_columnIndices.Length];
        var values = new double[_values.Length];
        for (var row = 0; row < RowCount; row++) {
            for (var index = _rowOffsets[row]; index < _rowOffsets[row + 1]; index++) {
                var target = next[_columnIndices[index]]++;
                columnIndices[target] = row;
                values[target] = _values[index];
            }
        }

        return new SparseMatrix(
            rowOffsets,
            columnIndices,
            values,
            ColumnCount,
            RowCount);
    }

    /// <summary>
    /// Solves a symmetric positive-definite system with the conjugate-gradient
    /// method starting from a zero initial guess.
    /// </summary>
    /// <remarks>
    /// Convergence is tested against
    /// <c>absoluteTolerance + relativeTolerance * ||rhs||</c>. A breakdown,
    /// non-positive curvature, non-finite arithmetic, or iteration limit
    /// returns false. Positive definiteness is not checked in advance.
    /// </remarks>
    public bool TryConjugateGradient(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryConjugateGradient(
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

    private bool TryConjugateGradientCore(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        out SparseIterativeStatus status,
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

        if (workspace.Length < checked(RowCount * 3)) {
            throw new ArgumentException(
                "The workspace must contain at least three times the order.",
                nameof(workspace));
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
        var order = RowCount;
        var residual = workspace[..order];
        var direction = workspace.Slice(order, order);
        var product = workspace.Slice(order * 2, order);
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
            direction[index] = value;
            rightHandSideNormSquared += value * value;
        }

        var tolerance = absoluteTolerance +
            (relativeTolerance * Math.Sqrt(rightHandSideNormSquared));
        var residualNorm = Math.Sqrt(rightHandSideNormSquared);
        if (residualNorm <= tolerance) {
            iterationCount = 0;
            status = SparseIterativeStatus.Succeeded;
            return true;
        }

        var residualDot = rightHandSideNormSquared;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            Multiply(direction, product);
            var curvature = 0.0;
            for (var index = 0; index < order; index++) {
                curvature += direction[index] * product[index];
            }

            if (!double.IsFinite(curvature)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            if (curvature <= 0) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            var alpha = residualDot / curvature;
            if (!double.IsFinite(alpha)) {
                iterationCount = iteration - 1;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            var nextResidualDot = 0.0;
            for (var index = 0; index < order; index++) {
                solution[index] += alpha * direction[index];
                residual[index] -= alpha * product[index];
                if (!double.IsFinite(solution[index]) ||
                    !double.IsFinite(residual[index])) {
                    iterationCount = iteration - 1;
                    status = SparseIterativeStatus.NonFiniteArithmetic;
                    return false;
                }

                nextResidualDot += residual[index] * residual[index];
            }

            residualNorm = Math.Sqrt(nextResidualDot);
            if (residualNorm <= tolerance) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }

            if (nextResidualDot == 0) {
                iterationCount = iteration;
                status = SparseIterativeStatus.Succeeded;
                return true;
            }

            var beta = nextResidualDot / residualDot;
            if (!double.IsFinite(beta)) {
                iterationCount = iteration;
                status = SparseIterativeStatus.NonFiniteArithmetic;
                return false;
            }

            for (var index = 0; index < order; index++) {
                direction[index] = residual[index] + (beta * direction[index]);
            }

            residualDot = nextResidualDot;
        }

        iterationCount = maximumIterations;
        status = SparseIterativeStatus.MaximumIterationsReached;
        return false;
    }
}
