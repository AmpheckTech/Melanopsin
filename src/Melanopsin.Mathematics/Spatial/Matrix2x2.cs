// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-by-two matrix using row-major storage and column-vector
/// multiplication.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Matrix2x2 :
    IMultiplyOperators<Matrix2x2, Matrix2x2, Matrix2x2>,
    IMultiplyOperators<Matrix2x2, float, Matrix2x2>,
    IDivisionOperators<Matrix2x2, float, Matrix2x2>,
    IMultiplicativeIdentity<Matrix2x2, Matrix2x2>,
    IEqualityOperators<Matrix2x2, Matrix2x2, bool> {
    public Matrix2x2(float m11, float m12, float m21, float m22) {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

    public static Matrix2x2 Identity => new(1, 0, 0, 1);

    public static Matrix2x2 MultiplicativeIdentity => Identity;

    public static Matrix2x2 FromStorage(
        ReadOnlySpan<float> source,
        MatrixStorageOrder storageOrder) {
        if (source.Length < 4) {
            throw new ArgumentException(
                "A 2x2 matrix requires four values.",
                nameof(source));
        }

        return storageOrder switch {
            MatrixStorageOrder.RowMajor => new Matrix2x2(
                source[0], source[1],
                source[2], source[3]),
            MatrixStorageOrder.ColumnMajor => new Matrix2x2(
                source[0], source[2],
                source[1], source[3]),
            _ => throw new ArgumentOutOfRangeException(nameof(storageOrder)),
        };
    }

    public float M11 { get; }

    public float M12 { get; }

    public float M21 { get; }

    public float M22 { get; }

    public Vector2 Row1 => new(M11, M12);

    public Vector2 Row2 => new(M21, M22);

    public Vector2 Column1 => new(M11, M21);

    public Vector2 Column2 => new(M12, M22);

    /// <summary>
    /// Gets the determinant using double intermediates before rounding to
    /// <see cref="float"/>.
    /// </summary>
    public float Determinant =>
        (float)(((double)M11 * M22) - ((double)M12 * M21));

    public static Matrix2x2 CreateRotation(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix2x2(
            cosine, -sine,
            sine, cosine);
    }

    public static Matrix2x2 CreateScale(float scale) {
        return new Matrix2x2(scale, 0, 0, scale);
    }

    public static Matrix2x2 CreateScale(Vector2 scale) {
        return new Matrix2x2(scale.X, 0, 0, scale.Y);
    }

    public Vector2 Transform(Vector2 value) {
        return new Vector2(
            (float)(((double)M11 * value.X) + ((double)M12 * value.Y)),
            (float)(((double)M21 * value.X) + ((double)M22 * value.Y)));
    }

    public void CopyTo(
        Span<float> destination,
        MatrixStorageOrder storageOrder) {
        if (destination.Length < 4) {
            throw new ArgumentException(
                "A 2x2 matrix requires four destination values.",
                nameof(destination));
        }

        switch (storageOrder) {
            case MatrixStorageOrder.RowMajor:
                destination[0] = M11;
                destination[1] = M12;
                destination[2] = M21;
                destination[3] = M22;
                break;
            case MatrixStorageOrder.ColumnMajor:
                destination[0] = M11;
                destination[1] = M21;
                destination[2] = M12;
                destination[3] = M22;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storageOrder));
        }
    }

    public Matrix2x2 Transpose() {
        return new Matrix2x2(M11, M21, M12, M22);
    }

    public Matrix2x2 Invert() {
        if (!TryInvert(out var inverse)) {
            throw new ArithmeticException("The matrix is singular.");
        }

        return inverse;
    }

    /// <summary>
    /// Attempts to invert the matrix without applying an absolute
    /// near-singularity threshold. Use <see cref="TryEstimateConditionNumber(out double)"/>
    /// when numerical accuracy must be assessed.
    /// </summary>
    /// <remarks>
    /// Determinant magnitude is scale-dependent. Inversion fails only for
    /// exact singularity in double precision, non-finite intermediates, or a
    /// result that cannot be represented as <see cref="float"/>.
    /// </remarks>
    public bool TryInvert(out Matrix2x2 inverse) {
        return TryInvert(out inverse, out _);
    }

    private bool TryInvertCore(out Matrix2x2 inverse) {
        double m11 = M11;
        double m12 = M12;
        double m21 = M21;
        double m22 = M22;
        var determinant = (m11 * m22) - (m12 * m21);
        if (!double.IsFinite(determinant) || determinant == 0) {
            inverse = default;
            return false;
        }

        var inverseDeterminant = 1 / determinant;
        var i11 = m22 * inverseDeterminant;
        var i12 = -m12 * inverseDeterminant;
        var i21 = -m21 * inverseDeterminant;
        var i22 = m11 * inverseDeterminant;
        if (!MatrixNumerics.IsRepresentableAsFloat(i11) ||
            !MatrixNumerics.IsRepresentableAsFloat(i12) ||
            !MatrixNumerics.IsRepresentableAsFloat(i21) ||
            !MatrixNumerics.IsRepresentableAsFloat(i22)) {
            inverse = default;
            return false;
        }

        inverse = new Matrix2x2(
            (float)i11,
            (float)i12,
            (float)i21,
            (float)i22);
        return true;
    }

    /// <summary>
    /// Estimates the condition number as the product of the matrix and inverse
    /// Frobenius norms. The estimate is scale invariant and is intended for
    /// caller-side accuracy decisions; it is not an exact two-norm condition
    /// number.
    /// </summary>
    public bool TryEstimateConditionNumber(out double conditionNumber) {
        return TryEstimateConditionNumber(out conditionNumber, out _);
    }

    public static Matrix2x2 Multiply(Matrix2x2 left, Matrix2x2 right) {
        return new Matrix2x2(
            (float)(((double)left.M11 * right.M11) + ((double)left.M12 * right.M21)),
            (float)(((double)left.M11 * right.M12) + ((double)left.M12 * right.M22)),
            (float)(((double)left.M21 * right.M11) + ((double)left.M22 * right.M21)),
            (float)(((double)left.M21 * right.M12) + ((double)left.M22 * right.M22)));
    }

    public static Vector2 Multiply(Matrix2x2 matrix, Vector2 value) {
        return matrix.Transform(value);
    }

    public static Matrix2x2 Multiply(Matrix2x2 matrix, float scale) {
        return new Matrix2x2(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale);
    }

    public static Matrix2x2 Multiply(float scale, Matrix2x2 matrix) {
        return Multiply(matrix, scale);
    }

    public static Matrix2x2 Divide(Matrix2x2 matrix, float divisor) {
        return new Matrix2x2(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor);
    }

    public static Matrix2x2 operator *(Matrix2x2 left, Matrix2x2 right) {
        return Multiply(left, right);
    }

    public static Vector2 operator *(Matrix2x2 matrix, Vector2 value) {
        return Multiply(matrix, value);
    }

    public static Matrix2x2 operator *(Matrix2x2 matrix, float scale) {
        return Multiply(matrix, scale);
    }

    public static Matrix2x2 operator *(float scale, Matrix2x2 matrix) {
        return Multiply(scale, matrix);
    }

    public static Matrix2x2 operator /(Matrix2x2 matrix, float divisor) {
        return Divide(matrix, divisor);
    }
}
