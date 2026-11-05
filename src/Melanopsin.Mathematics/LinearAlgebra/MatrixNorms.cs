// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Norms for dense row-major matrices.
/// </summary>
public static class MatrixNorms {
    /// <summary>
    /// Computes the Frobenius norm using scaled accumulation.
    /// </summary>
    public static double FrobeniusNorm(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var elementCount = rows * columns;
        var scale = 0.0;
        var sumSquares = 1.0;
        for (var index = 0; index < elementCount; index++) {
            AccumulateSquare(Math.Abs(source[index]), ref scale, ref sumSquares);
        }

        return scale == 0 ? 0 : scale * Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Computes the maximum absolute column sum.
    /// </summary>
    public static double OneNorm(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var maximum = 0.0;
        for (var column = 0; column < columns; column++) {
            var sum = 0.0;
            for (var row = 0; row < rows; row++) {
                sum += Math.Abs(source[(row * columns) + column]);
            }

            maximum = Math.Max(maximum, sum);
        }

        return maximum;
    }

    /// <summary>
    /// Computes the maximum absolute row sum.
    /// </summary>
    public static double InfinityNorm(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var maximum = 0.0;
        for (var row = 0; row < rows; row++) {
            var sum = 0.0;
            var rowOffset = row * columns;
            for (var column = 0; column < columns; column++) {
                sum += Math.Abs(source[rowOffset + column]);
            }

            maximum = Math.Max(maximum, sum);
        }

        return maximum;
    }

    /// <summary>
    /// Computes the largest absolute matrix entry.
    /// </summary>
    public static double MaxAbsolute(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var elementCount = rows * columns;
        var maximum = 0.0;
        for (var index = 0; index < elementCount; index++) {
            maximum = Math.Max(maximum, Math.Abs(source[index]));
        }

        return maximum;
    }

    /// <summary>
    /// Computes the Frobenius norm of a complex matrix.
    /// </summary>
    public static double FrobeniusNorm(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var elementCount = rows * columns;
        var scale = 0.0;
        var sumSquares = 1.0;
        for (var index = 0; index < elementCount; index++) {
            AccumulateSquare(
                source[index].Magnitude,
                ref scale,
                ref sumSquares);
        }

        return scale == 0 ? 0 : scale * Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Computes the maximum absolute complex column sum.
    /// </summary>
    public static double OneNorm(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var maximum = 0.0;
        for (var column = 0; column < columns; column++) {
            var sum = 0.0;
            for (var row = 0; row < rows; row++) {
                sum += source[(row * columns) + column].Magnitude;
            }

            maximum = Math.Max(maximum, sum);
        }

        return maximum;
    }

    /// <summary>
    /// Computes the maximum absolute complex row sum.
    /// </summary>
    public static double InfinityNorm(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var maximum = 0.0;
        for (var row = 0; row < rows; row++) {
            var sum = 0.0;
            var rowOffset = row * columns;
            for (var column = 0; column < columns; column++) {
                sum += source[rowOffset + column].Magnitude;
            }

            maximum = Math.Max(maximum, sum);
        }

        return maximum;
    }

    /// <summary>
    /// Computes the largest complex magnitude.
    /// </summary>
    public static double MaxAbsolute(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        Validate(source, rows, columns);
        var elementCount = rows * columns;
        var maximum = 0.0;
        for (var index = 0; index < elementCount; index++) {
            maximum = Math.Max(maximum, source[index].Magnitude);
        }

        return maximum;
    }

    private static void AccumulateSquare(
        double absolute,
        ref double scale,
        ref double sumSquares) {
        if (absolute == 0) {
            return;
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

    private static void Validate(
        ReadOnlySpan<double> source,
        int rows,
        int columns) {
        DenseMatrixValidation.Validate(rows, columns);
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }
    }

    private static void Validate(
        ReadOnlySpan<Complex> source,
        int rows,
        int columns) {
        DenseMatrixValidation.Validate(rows, columns);
        var elementCount = checked(rows * columns);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough values.",
                nameof(source));
        }
    }
}
