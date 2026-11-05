// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Allocation-free span kernels and convenience operations for dense
/// row-major matrices using column-vector semantics.
/// </summary>
public static partial class DenseMatrix {
    /// <summary>
    /// Computes <c>C = A B</c> for row-major matrices.
    /// </summary>
    public static void Multiply(
        ReadOnlySpan<double> left,
        int leftRows,
        int leftColumns,
        ReadOnlySpan<double> right,
        int rightRows,
        int rightColumns,
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
        if (left.Length < leftCount || right.Length < rightCount) {
            throw new ArgumentException(
                "A source matrix does not contain enough values.");
        }

        if (destination.Length < destinationCount) {
            throw new ArgumentException(
                "The destination matrix does not contain enough values.",
                nameof(destination));
        }

        if (left.Overlaps(destination) || right.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either source matrix.",
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
    }

    /// <summary>
    /// Computes a matrix-vector product for a row-major matrix.
    /// </summary>
    public static void MultiplyVector(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> vector,
        Span<double> destination) {
        ValidateDimensions(rows, columns, nameof(matrix));
        var matrixCount = checked(rows * columns);
        if (matrix.Length < matrixCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        if (vector.Length < columns) {
            throw new ArgumentException(
                "The vector does not contain enough values.",
                nameof(vector));
        }

        if (destination.Length < rows) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (vector.Overlaps(destination) || matrix.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap an input buffer.",
                nameof(destination));
        }

        for (var row = 0; row < rows; row++) {
            var value = 0.0;
            var rowOffset = row * columns;
            for (var column = 0; column < columns; column++) {
                value += matrix[rowOffset + column] * vector[column];
            }

            destination[row] = value;
        }
    }

    /// <summary>
    /// Copies the transpose of a row-major matrix to a destination.
    /// </summary>
    public static void Transpose(
        ReadOnlySpan<double> source,
        int rows,
        int columns,
        Span<double> destination) {
        ValidateDimensions(rows, columns, nameof(source));
        var sourceCount = checked(rows * columns);
        if (source.Length < sourceCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        if (destination.Length < sourceCount) {
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
            for (var column = 0; column < columns; column++) {
                destination[(column * rows) + row] =
                    source[(row * columns) + column];
            }
        }
    }

    /// <summary>
    /// Adds two equally sized row-major matrices.
    /// </summary>
    public static void Add(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right,
        Span<double> destination,
        int elementCount) {
        ApplyBinary(left, right, destination, elementCount, add: true);
    }

    /// <summary>
    /// Subtracts two equally sized row-major matrices.
    /// </summary>
    public static void Subtract(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right,
        Span<double> destination,
        int elementCount) {
        ApplyBinary(left, right, destination, elementCount, add: false);
    }

    /// <summary>
    /// Multiplies every row-major matrix value by a scalar.
    /// </summary>
    public static void Scale(
        ReadOnlySpan<double> source,
        int elementCount,
        double scale,
        Span<double> destination) {
        ValidateElementCount(elementCount);
        if (source.Length < elementCount ||
            destination.Length < elementCount) {
            throw new ArgumentException(
                "A matrix buffer does not contain enough values.");
        }

        if (source.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the source.",
                nameof(destination));
        }

        for (var index = 0; index < elementCount; index++) {
            destination[index] = source[index] * scale;
        }
    }

    /// <summary>
    /// Determines whether every matrix value is finite.
    /// </summary>
    public static bool IsFinite(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        ValidateDimensions(rows, columns, nameof(source));
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(source[index])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes <c>C = A B</c> for row-major complex matrices.
    /// </summary>
    public static void Multiply(
        ReadOnlySpan<Complex> left,
        int leftRows,
        int leftColumns,
        ReadOnlySpan<Complex> right,
        int rightRows,
        int rightColumns,
        Span<Complex> destination) {
        ValidateDimensions(leftRows, leftColumns, "left");
        ValidateDimensions(rightRows, rightColumns, "right");
        if (leftColumns != rightRows) {
            throw new ArgumentException(
                "The left column count must equal the right row count.");
        }

        var leftCount = checked(leftRows * leftColumns);
        var rightCount = checked(rightRows * rightColumns);
        var destinationCount = checked(leftRows * rightColumns);
        if (left.Length < leftCount || right.Length < rightCount) {
            throw new ArgumentException(
                "A source matrix does not contain enough values.");
        }

        if (destination.Length < destinationCount) {
            throw new ArgumentException(
                "The destination matrix does not contain enough values.",
                nameof(destination));
        }

        if (left.Overlaps(destination) || right.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either source matrix.",
                nameof(destination));
        }

        destination[..destinationCount].Clear();
        for (var row = 0; row < leftRows; row++) {
            var leftRowOffset = row * leftColumns;
            var destinationRowOffset = row * rightColumns;
            for (var inner = 0; inner < leftColumns; inner++) {
                var leftValue = left[leftRowOffset + inner];
                if (leftValue == Complex.Zero) {
                    continue;
                }

                var rightRowOffset = inner * rightColumns;
                for (var column = 0; column < rightColumns; column++) {
                    destination[destinationRowOffset + column] +=
                        leftValue * right[rightRowOffset + column];
                }
            }
        }
    }

    /// <summary>
    /// Computes a complex matrix-vector product for a row-major matrix.
    /// </summary>
    public static void MultiplyVector(
        ReadOnlySpan<Complex> matrix,
        int rows,
        int columns,
        ReadOnlySpan<Complex> vector,
        Span<Complex> destination) {
        ValidateDimensions(rows, columns, nameof(matrix));
        var matrixCount = checked(rows * columns);
        if (matrix.Length < matrixCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        if (vector.Length < columns) {
            throw new ArgumentException(
                "The vector does not contain enough values.",
                nameof(vector));
        }

        if (destination.Length < rows) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (vector.Overlaps(destination) || matrix.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap an input buffer.",
                nameof(destination));
        }

        for (var row = 0; row < rows; row++) {
            var value = Complex.Zero;
            var rowOffset = row * columns;
            for (var column = 0; column < columns; column++) {
                value += matrix[rowOffset + column] * vector[column];
            }

            destination[row] = value;
        }
    }

    /// <summary>
    /// Copies the conjugate transpose of a row-major complex matrix to a
    /// destination.
    /// </summary>
    public static void ConjugateTranspose(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns,
        Span<Complex> destination) {
        ValidateDimensions(rows, columns, nameof(source));
        var sourceCount = checked(rows * columns);
        if (source.Length < sourceCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        if (destination.Length < sourceCount) {
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
            for (var column = 0; column < columns; column++) {
                destination[(column * rows) + row] =
                    source[(row * columns) + column].Conjugate();
            }
        }
    }

    /// <summary>
    /// Determines whether every complex matrix value is finite.
    /// </summary>
    public static bool IsFinite(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        ValidateDimensions(rows, columns, nameof(source));
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!source[index].IsFinite) {
                return false;
            }
        }

        return true;
    }

    private static void ApplyBinary(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right,
        Span<double> destination,
        int elementCount,
        bool add) {
        ValidateElementCount(elementCount);
        if (left.Length < elementCount ||
            right.Length < elementCount ||
            destination.Length < elementCount) {
            throw new ArgumentException(
                "A matrix buffer does not contain enough values.");
        }

        if (left.Overlaps(destination) || right.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        for (var index = 0; index < elementCount; index++) {
            destination[index] = add
                ? left[index] + right[index]
                : left[index] - right[index];
        }
    }

    private static void ValidateDimensions(
        int rows,
        int columns,
        string parameterName) {
        if (rows <= 0) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rows,
                "The row count must be positive.");
        }

        if (columns <= 0) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                columns,
                "The column count must be positive.");
        }
    }

    private static void ValidateElementCount(int elementCount) {
        if (elementCount <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(elementCount),
                elementCount,
                "The element count must be positive.");
        }
    }
}
