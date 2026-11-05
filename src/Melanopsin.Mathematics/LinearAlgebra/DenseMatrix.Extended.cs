namespace Melanopsin.Mathematics;

public static partial class DenseMatrix {
    /// <summary>
    /// Computes the trace of a square row-major matrix.
    /// </summary>
    public static double Trace(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        ValidateDimensions(rows, columns, nameof(source));
        if (rows != columns) {
            throw new ArgumentException(
                "Trace requires a square matrix.",
                nameof(columns));
        }

        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        var result = 0.0;
        for (var index = 0; index < rows; index++) {
            result += source[(index * columns) + index];
        }

        return result;
    }

    /// <summary>
    /// Copies one row from a row-major matrix.
    /// </summary>
    public static void CopyRow(
        ReadOnlySpan<double> source,
        int rows,
        int columns,
        int rowIndex,
        Span<double> destination) {
        ValidateDimensions(rows, columns, nameof(source));
        ValidateIndex(rowIndex, rows, nameof(rowIndex));
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        if (destination.Length < columns) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (source.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the source.",
                nameof(destination));
        }

        source.Slice(rowIndex * columns, columns).CopyTo(destination);
    }

    /// <summary>
    /// Copies one column from a row-major matrix.
    /// </summary>
    public static void CopyColumn(
        ReadOnlySpan<double> source,
        int rows,
        int columns,
        int columnIndex,
        Span<double> destination) {
        ValidateDimensions(rows, columns, nameof(source));
        ValidateIndex(columnIndex, columns, nameof(columnIndex));
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        if (destination.Length < rows) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (source.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the source.",
                nameof(destination));
        }

        for (var row = 0; row < rows; row++) {
            destination[row] = source[(row * columns) + columnIndex];
        }
    }

    /// <summary>
    /// Swaps two rows in a mutable row-major matrix.
    /// </summary>
    public static void SwapRows(
        Span<double> matrix,
        int rows,
        int columns,
        int firstRow,
        int secondRow) {
        ValidateDimensions(rows, columns, nameof(matrix));
        ValidateIndex(firstRow, rows, nameof(firstRow));
        ValidateIndex(secondRow, rows, nameof(secondRow));
        var elementCount = checked(rows * columns);
        if (matrix.Length < elementCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        if (firstRow == secondRow) {
            return;
        }

        for (var column = 0; column < columns; column++) {
            var first = (firstRow * columns) + column;
            var second = (secondRow * columns) + column;
            (matrix[first], matrix[second]) = (matrix[second], matrix[first]);
        }
    }

    /// <summary>
    /// Swaps two columns in a mutable row-major matrix.
    /// </summary>
    public static void SwapColumns(
        Span<double> matrix,
        int rows,
        int columns,
        int firstColumn,
        int secondColumn) {
        ValidateDimensions(rows, columns, nameof(matrix));
        ValidateIndex(firstColumn, columns, nameof(firstColumn));
        ValidateIndex(secondColumn, columns, nameof(secondColumn));
        var elementCount = checked(rows * columns);
        if (matrix.Length < elementCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        if (firstColumn == secondColumn) {
            return;
        }

        for (var row = 0; row < rows; row++) {
            var first = (row * columns) + firstColumn;
            var second = (row * columns) + secondColumn;
            (matrix[first], matrix[second]) = (matrix[second], matrix[first]);
        }
    }

    /// <summary>
    /// Computes <c>destination = source + scale * addend</c> elementwise.
    /// </summary>
    public static void AddScaled(
        ReadOnlySpan<double> source,
        ReadOnlySpan<double> addend,
        double scale,
        Span<double> destination,
        int elementCount) {
        ValidateElementCount(elementCount);
        if (source.Length < elementCount ||
            addend.Length < elementCount ||
            destination.Length < elementCount) {
            throw new ArgumentException(
                "A matrix buffer does not contain enough values.");
        }

        if (source.Overlaps(destination) ||
            addend.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        for (var index = 0; index < elementCount; index++) {
            destination[index] = source[index] + (scale * addend[index]);
        }
    }

    /// <summary>
    /// Computes <c>destination = A B + addend</c> for row-major matrices.
    /// </summary>
    /// <remarks>
    /// The addend has the dimensions of the product and is not modified. The
    /// destination must not overlap any input.
    /// </remarks>
    public static void MultiplyAdd(
        ReadOnlySpan<double> left,
        int leftRows,
        int leftColumns,
        ReadOnlySpan<double> right,
        int rightRows,
        int rightColumns,
        ReadOnlySpan<double> addend,
        Span<double> destination) {
        ValidateDimensions(leftRows, leftColumns, "left");
        ValidateDimensions(rightRows, rightColumns, "right");
        if (leftColumns != rightRows) {
            throw new ArgumentException(
                "The left column count must equal the right row count.");
        }

        var leftCount = checked(leftRows * leftColumns);
        var rightCount = checked(rightRows * rightColumns);
        var destinationCount = checked(leftRows * rightColumns);
        if (left.Length < leftCount ||
            right.Length < rightCount ||
            addend.Length < destinationCount) {
            throw new ArgumentException(
                "An input matrix does not contain enough values.");
        }

        if (destination.Length < destinationCount) {
            throw new ArgumentException(
                "The destination matrix does not contain enough values.",
                nameof(destination));
        }

        if (left.Overlaps(destination) ||
            right.Overlaps(destination) ||
            addend.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap any input matrix.",
                nameof(destination));
        }

        destination[..destinationCount].Clear();
        for (var row = 0; row < leftRows; row++) {
            var leftRowOffset = row * leftColumns;
            var destinationRowOffset = row * rightColumns;
            for (var inner = 0; inner < leftColumns; inner++) {
                var leftValue = left[leftRowOffset + inner];
                if (leftValue == 0) {
                    continue;
                }

                var rightRowOffset = inner * rightColumns;
                for (var column = 0; column < rightColumns; column++) {
                    destination[destinationRowOffset + column] +=
                        leftValue * right[rightRowOffset + column];
                }
            }
        }

        for (var index = 0; index < destinationCount; index++) {
            destination[index] += addend[index];
        }
    }

    /// <summary>
    /// Gets the exact SVD rank: the count of strictly positive singular values.
    /// </summary>
    /// <remarks>
    /// Exact zero tests in floating point are generally unsuitable for
    /// approximate data. Prefer the overload with an explicit relative
    /// tolerance when the data has a known numerical scale.
    /// </remarks>
    public static int Rank(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        return new SvdDecomposition(source, rows, columns).Rank;
    }

    /// <summary>
    /// Counts singular values strictly greater than the largest singular value
    /// times <paramref name="relativeTolerance"/>.
    /// </summary>
    /// <param name="source">The row-major matrix values.</param>
    /// <param name="rows">The number of matrix rows.</param>
    /// <param name="columns">The number of matrix columns.</param>
    /// <param name="relativeTolerance">
    /// A finite non-negative tolerance relative to the largest singular value.
    /// This is scale-independent; the caller chooses the precision policy.
    /// </param>
    public static int Rank(
        ReadOnlySpan<double> source,
        int rows,
        int columns,
        double relativeTolerance) {
        return new SvdDecomposition(source, rows, columns)
            .CountRank(relativeTolerance);
    }

    /// <summary>
    /// Computes the 2-norm condition number from the singular values.
    /// </summary>
    /// <remarks>
    /// A zero smallest singular value produces positive infinity. This value
    /// is a diagnostic and is never used by inversion APIs as an implicit
    /// near-singularity cutoff.
    /// </remarks>
    public static double ConditionNumber(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        return new SvdDecomposition(source, rows, columns).ConditionNumber;
    }

    private static void ValidateIndex(
        int index,
        int exclusiveUpperBound,
        string parameterName) {
        if (index < 0 || index >= exclusiveUpperBound) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
