// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A four-by-four matrix using row-major storage and column-vector
/// multiplication. Translation is stored in the fourth column.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Matrix4x4 :
    IMultiplyOperators<Matrix4x4, Matrix4x4, Matrix4x4>,
    IMultiplyOperators<Matrix4x4, float, Matrix4x4>,
    IDivisionOperators<Matrix4x4, float, Matrix4x4>,
    IMultiplicativeIdentity<Matrix4x4, Matrix4x4>,
    IEqualityOperators<Matrix4x4, Matrix4x4, bool> {
    public Matrix4x4(
        float m11,
        float m12,
        float m13,
        float m14,
        float m21,
        float m22,
        float m23,
        float m24,
        float m31,
        float m32,
        float m33,
        float m34,
        float m41,
        float m42,
        float m43,
        float m44) {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M14 = m14;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M24 = m24;
        M31 = m31;
        M32 = m32;
        M33 = m33;
        M34 = m34;
        M41 = m41;
        M42 = m42;
        M43 = m43;
        M44 = m44;
    }

    public static Matrix4x4 Identity => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public static Matrix4x4 MultiplicativeIdentity => Identity;

    public static Matrix4x4 FromStorage(
        ReadOnlySpan<float> source,
        MatrixStorageOrder storageOrder) {
        if (source.Length < 16) {
            throw new ArgumentException(
                "A 4x4 matrix requires sixteen values.",
                nameof(source));
        }

        return storageOrder switch {
            MatrixStorageOrder.RowMajor => new Matrix4x4(
                source[0], source[1], source[2], source[3],
                source[4], source[5], source[6], source[7],
                source[8], source[9], source[10], source[11],
                source[12], source[13], source[14], source[15]),
            MatrixStorageOrder.ColumnMajor => new Matrix4x4(
                source[0], source[4], source[8], source[12],
                source[1], source[5], source[9], source[13],
                source[2], source[6], source[10], source[14],
                source[3], source[7], source[11], source[15]),
            _ => throw new ArgumentOutOfRangeException(nameof(storageOrder)),
        };
    }

    public float M11 { get; }

    public float M12 { get; }

    public float M13 { get; }

    public float M14 { get; }

    public float M21 { get; }

    public float M22 { get; }

    public float M23 { get; }

    public float M24 { get; }

    public float M31 { get; }

    public float M32 { get; }

    public float M33 { get; }

    public float M34 { get; }

    public float M41 { get; }

    public float M42 { get; }

    public float M43 { get; }

    public float M44 { get; }

    public Vector4 Row1 => new(M11, M12, M13, M14);

    public Vector4 Row2 => new(M21, M22, M23, M24);

    public Vector4 Row3 => new(M31, M32, M33, M34);

    public Vector4 Row4 => new(M41, M42, M43, M44);

    public Vector4 Column1 => new(M11, M21, M31, M41);

    public Vector4 Column2 => new(M12, M22, M32, M42);

    public Vector4 Column3 => new(M13, M23, M33, M43);

    public Vector4 Column4 => new(M14, M24, M34, M44);

    public static Matrix4x4 CreateTranslation(Vector3 translation) {
        return new Matrix4x4(
            1, 0, 0, translation.X,
            0, 1, 0, translation.Y,
            0, 0, 1, translation.Z,
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreateScale(float scale) {
        return CreateScale(new Vector3(scale, scale, scale));
    }

    public static Matrix4x4 CreateScale(Vector3 scale) {
        return new Matrix4x4(
            scale.X, 0, 0, 0,
            0, scale.Y, 0, 0,
            0, 0, scale.Z, 0,
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreateRotationX(float radians) {
        var rotation = Matrix3x3.CreateRotationX(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateRotationY(float radians) {
        var rotation = Matrix3x3.CreateRotationY(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateRotationZ(float radians) {
        var rotation = Matrix3x3.CreateRotationZ(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateFromAxisAngle(
        Vector3 axis,
        float radians) {
        return CreateFromQuaternion(Quaternion.CreateFromAxisAngle(axis, radians));
    }

    public static Matrix4x4 CreateFromQuaternion(Quaternion quaternion) {
        return FromRotation(Matrix3x3.CreateFromQuaternion(quaternion));
    }

    /// <summary>
    /// Multiplies a homogeneous column vector using double intermediates before
    /// rounding each component to <see cref="float"/>.
    /// </summary>
    public Vector4 Transform(Vector4 value) {
        return new Vector4(
            (float)(((double)M11 * value.X) + ((double)M12 * value.Y) +
                ((double)M13 * value.Z) + ((double)M14 * value.W)),
            (float)(((double)M21 * value.X) + ((double)M22 * value.Y) +
                ((double)M23 * value.Z) + ((double)M24 * value.W)),
            (float)(((double)M31 * value.X) + ((double)M32 * value.Y) +
                ((double)M33 * value.Z) + ((double)M34 * value.W)),
            (float)(((double)M41 * value.X) + ((double)M42 * value.Y) +
                ((double)M43 * value.Z) + ((double)M44 * value.W)));
    }

    /// <summary>
    /// Transforms a point through the full homogeneous matrix and performs the
    /// perspective divide.
    /// </summary>
    /// <remarks>
    /// For affine matrices this is equivalent to applying the linear part and
    /// translation because W remains one. A point is not representable as a
    /// <see cref="Vector3"/> when its transformed W component is zero or
    /// non-finite, or when a projected coordinate is non-finite. Use
    /// <see cref="Transform(Vector4)"/> when the homogeneous coordinates must be
    /// retained.
    /// </remarks>
    /// <exception cref="ArithmeticException">
    /// The transformed point cannot be represented as finite Cartesian
    /// coordinates.
    /// </exception>
    public Vector3 TransformPoint(Vector3 value) {
        if (!TryTransformPoint(value, out var result)) {
            throw new ArithmeticException(
                "The transformed point cannot be represented as finite Cartesian coordinates.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a point through the full homogeneous matrix and
    /// perform the perspective divide.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when the transformed W component is zero
    /// or non-finite, or when a projected coordinate is non-finite. In that case
    /// <paramref name="result"/> is set to <see langword="default"/>.
    /// </remarks>
    public bool TryTransformPoint(Vector3 value, out Vector3 result) {
        return TryTransformPoint(value, out result, out _);
    }

    /// <summary>
    /// Transforms a direction through a matrix that is affine within the default
    /// relative tolerance. Translation is ignored.
    /// </summary>
    /// <remarks>
    /// A projective transform has no point-independent direction mapping. This
    /// method therefore rejects matrices whose fourth row is not affine after
    /// normalization by M44. The relative residual
    /// <c>max(|M41|, |M42|, |M43|) / |M44|</c> is compared with
    /// <see cref="ScalarMath.DefaultSingleTolerance"/>; pass an explicit tolerance through the
    /// overload when a domain requires a different threshold. Transform two
    /// points and subtract their results when a projected direction is required.
    /// </remarks>
    /// <exception cref="ArithmeticException">
    /// The normalized matrix is not affine within the default tolerance, or the
    /// transformed direction cannot be represented as finite values.
    /// </exception>
    public Vector3 TransformAffineDirection(Vector3 value) {
        return TransformAffineDirection(value, ScalarMath.DefaultSingleTolerance);
    }

    /// <summary>
    /// Transforms a direction through a matrix that is affine within the given
    /// relative tolerance. Translation is ignored.
    /// </summary>
    /// <param name="value">The direction to transform.</param>
    /// <param name="relativeTolerance">
    /// The maximum allowed normalized fourth-row residual. Must be finite and
    /// non-negative. A value of zero requires an exact affine matrix.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="relativeTolerance"/> is negative or non-finite.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The normalized matrix is not affine within
    /// <paramref name="relativeTolerance"/>, or the transformed direction cannot
    /// be represented as finite values.
    /// </exception>
    public Vector3 TransformAffineDirection(
        Vector3 value,
        float relativeTolerance) {
        if (!TryTransformAffineDirection(value, relativeTolerance, out var result)) {
            throw new ArithmeticException(
                "TransformAffineDirection requires an affine matrix within the requested tolerance.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a direction through a matrix that is affine within
    /// the given relative tolerance.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the matrix passes the relative affine test and
    /// the result is finite; otherwise, <see langword="false"/> with
    /// <paramref name="result"/> set to <see langword="default"/>.
    /// </returns>
    public bool TryTransformAffineDirection(
        Vector3 value,
        float relativeTolerance,
        out Vector3 result) {
        return TryTransformAffineDirection(
            value,
            relativeTolerance,
            out result,
            out _);
    }

    public Matrix4x4 Transpose() {
        return new Matrix4x4(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44);
    }

    public void CopyTo(
        Span<float> destination,
        MatrixStorageOrder storageOrder) {
        if (destination.Length < 16) {
            throw new ArgumentException(
                "A 4x4 matrix requires sixteen destination values.",
                nameof(destination));
        }

        switch (storageOrder) {
            case MatrixStorageOrder.RowMajor:
                destination[0] = M11;
                destination[1] = M12;
                destination[2] = M13;
                destination[3] = M14;
                destination[4] = M21;
                destination[5] = M22;
                destination[6] = M23;
                destination[7] = M24;
                destination[8] = M31;
                destination[9] = M32;
                destination[10] = M33;
                destination[11] = M34;
                destination[12] = M41;
                destination[13] = M42;
                destination[14] = M43;
                destination[15] = M44;
                break;
            case MatrixStorageOrder.ColumnMajor:
                destination[0] = M11;
                destination[1] = M21;
                destination[2] = M31;
                destination[3] = M41;
                destination[4] = M12;
                destination[5] = M22;
                destination[6] = M32;
                destination[7] = M42;
                destination[8] = M13;
                destination[9] = M23;
                destination[10] = M33;
                destination[11] = M43;
                destination[12] = M14;
                destination[13] = M24;
                destination[14] = M34;
                destination[15] = M44;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storageOrder));
        }
    }

    public Matrix4x4 Invert() {
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
    public bool TryInvert(out Matrix4x4 inverse) {
        return TryInvert(out inverse, out _);
    }

    private bool TryInvertCore(out Matrix4x4 inverse) {
        Span<double> source = stackalloc double[16] {
            M11, M12, M13, M14,
            M21, M22, M23, M24,
            M31, M32, M33, M34,
            M41, M42, M43, M44,
        };
        Span<double> result = stackalloc double[16] {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1,
        };

        var scale = 0d;
        for (var index = 0; index < source.Length; index++) {
            var value = source[index];
            if (!double.IsFinite(value)) {
                inverse = default;
                return false;
            }

            scale = Math.Max(scale, Math.Abs(value));
        }

        if (scale == 0) {
            inverse = default;
            return false;
        }

        for (var index = 0; index < source.Length; index++) {
            source[index] /= scale;
        }

        for (var column = 0; column < 4; column++) {
            var pivotRow = column;
            var pivotMagnitude = Math.Abs(source[(column * 4) + column]);
            for (var row = column + 1; row < 4; row++) {
                var magnitude = Math.Abs(source[(row * 4) + column]);
                if (magnitude > pivotMagnitude) {
                    pivotMagnitude = magnitude;
                    pivotRow = row;
                }
            }

            if (!double.IsFinite(pivotMagnitude) || pivotMagnitude == 0) {
                inverse = default;
                return false;
            }

            if (pivotRow != column) {
                SwapRows(source, pivotRow, column);
                SwapRows(result, pivotRow, column);
            }

            var pivotIndex = (column * 4) + column;
            var inversePivot = 1 / source[pivotIndex];
            if (!double.IsFinite(inversePivot)) {
                inverse = default;
                return false;
            }

            ScaleRow(source, column, inversePivot);
            ScaleRow(result, column, inversePivot);

            for (var row = 0; row < 4; row++) {
                if (row == column) {
                    continue;
                }

                var factor = source[(row * 4) + column];
                if (factor == 0) {
                    continue;
                }

                SubtractScaledRow(source, row, column, factor);
                SubtractScaledRow(result, row, column, factor);
            }
        }

        for (var index = 0; index < result.Length; index++) {
            result[index] /= scale;
            if (!MatrixNumerics.IsRepresentableAsFloat(result[index])) {
                inverse = default;
                return false;
            }
        }

        inverse = new Matrix4x4(
            (float)result[0], (float)result[1], (float)result[2], (float)result[3],
            (float)result[4], (float)result[5], (float)result[6], (float)result[7],
            (float)result[8], (float)result[9], (float)result[10], (float)result[11],
            (float)result[12], (float)result[13], (float)result[14], (float)result[15]);
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

    public static Matrix4x4 operator *(
        Matrix4x4 left,
        Matrix4x4 right) {
        return Multiply(left, right);
    }

    public static Matrix4x4 Multiply(Matrix4x4 left, Matrix4x4 right) {
        return new Matrix4x4(
            (float)(((double)left.M11 * right.M11) + ((double)left.M12 * right.M21) + ((double)left.M13 * right.M31) + ((double)left.M14 * right.M41)),
            (float)(((double)left.M11 * right.M12) + ((double)left.M12 * right.M22) + ((double)left.M13 * right.M32) + ((double)left.M14 * right.M42)),
            (float)(((double)left.M11 * right.M13) + ((double)left.M12 * right.M23) + ((double)left.M13 * right.M33) + ((double)left.M14 * right.M43)),
            (float)(((double)left.M11 * right.M14) + ((double)left.M12 * right.M24) + ((double)left.M13 * right.M34) + ((double)left.M14 * right.M44)),
            (float)(((double)left.M21 * right.M11) + ((double)left.M22 * right.M21) + ((double)left.M23 * right.M31) + ((double)left.M24 * right.M41)),
            (float)(((double)left.M21 * right.M12) + ((double)left.M22 * right.M22) + ((double)left.M23 * right.M32) + ((double)left.M24 * right.M42)),
            (float)(((double)left.M21 * right.M13) + ((double)left.M22 * right.M23) + ((double)left.M23 * right.M33) + ((double)left.M24 * right.M43)),
            (float)(((double)left.M21 * right.M14) + ((double)left.M22 * right.M24) + ((double)left.M23 * right.M34) + ((double)left.M24 * right.M44)),
            (float)(((double)left.M31 * right.M11) + ((double)left.M32 * right.M21) + ((double)left.M33 * right.M31) + ((double)left.M34 * right.M41)),
            (float)(((double)left.M31 * right.M12) + ((double)left.M32 * right.M22) + ((double)left.M33 * right.M32) + ((double)left.M34 * right.M42)),
            (float)(((double)left.M31 * right.M13) + ((double)left.M32 * right.M23) + ((double)left.M33 * right.M33) + ((double)left.M34 * right.M43)),
            (float)(((double)left.M31 * right.M14) + ((double)left.M32 * right.M24) + ((double)left.M33 * right.M34) + ((double)left.M34 * right.M44)),
            (float)(((double)left.M41 * right.M11) + ((double)left.M42 * right.M21) + ((double)left.M43 * right.M31) + ((double)left.M44 * right.M41)),
            (float)(((double)left.M41 * right.M12) + ((double)left.M42 * right.M22) + ((double)left.M43 * right.M32) + ((double)left.M44 * right.M42)),
            (float)(((double)left.M41 * right.M13) + ((double)left.M42 * right.M23) + ((double)left.M43 * right.M33) + ((double)left.M44 * right.M43)),
            (float)(((double)left.M41 * right.M14) + ((double)left.M42 * right.M24) + ((double)left.M43 * right.M34) + ((double)left.M44 * right.M44)));
    }

    public static Vector4 Multiply(Matrix4x4 matrix, Vector4 value) {
        return matrix.Transform(value);
    }

    public static Matrix4x4 Multiply(Matrix4x4 matrix, float scale) {
        return new Matrix4x4(
            matrix.M11 * scale, matrix.M12 * scale, matrix.M13 * scale, matrix.M14 * scale,
            matrix.M21 * scale, matrix.M22 * scale, matrix.M23 * scale, matrix.M24 * scale,
            matrix.M31 * scale, matrix.M32 * scale, matrix.M33 * scale, matrix.M34 * scale,
            matrix.M41 * scale, matrix.M42 * scale, matrix.M43 * scale, matrix.M44 * scale);
    }

    public static Matrix4x4 Multiply(float scale, Matrix4x4 matrix) {
        return Multiply(matrix, scale);
    }

    public static Matrix4x4 Divide(Matrix4x4 matrix, float divisor) {
        return new Matrix4x4(
            matrix.M11 / divisor, matrix.M12 / divisor, matrix.M13 / divisor, matrix.M14 / divisor,
            matrix.M21 / divisor, matrix.M22 / divisor, matrix.M23 / divisor, matrix.M24 / divisor,
            matrix.M31 / divisor, matrix.M32 / divisor, matrix.M33 / divisor, matrix.M34 / divisor,
            matrix.M41 / divisor, matrix.M42 / divisor, matrix.M43 / divisor, matrix.M44 / divisor);
    }

    public static Vector4 operator *(Matrix4x4 matrix, Vector4 value) {
        return Multiply(matrix, value);
    }

    public static Matrix4x4 operator *(Matrix4x4 matrix, float scale) {
        return Multiply(matrix, scale);
    }

    public static Matrix4x4 operator *(float scale, Matrix4x4 matrix) {
        return Multiply(scale, matrix);
    }

    public static Matrix4x4 operator /(Matrix4x4 matrix, float divisor) {
        return Divide(matrix, divisor);
    }

    private static Matrix4x4 FromRotation(Matrix3x3 rotation) {
        return new Matrix4x4(
            rotation.M11, rotation.M12, rotation.M13, 0,
            rotation.M21, rotation.M22, rotation.M23, 0,
            rotation.M31, rotation.M32, rotation.M33, 0,
            0, 0, 0, 1);
    }

    private static void SwapRows(Span<double> matrix, int first, int second) {
        for (var column = 0; column < 4; column++) {
            var firstIndex = (first * 4) + column;
            var secondIndex = (second * 4) + column;
            (matrix[firstIndex], matrix[secondIndex]) =
                (matrix[secondIndex], matrix[firstIndex]);
        }
    }

    private static void ScaleRow(Span<double> matrix, int row, double scale) {
        for (var column = 0; column < 4; column++) {
            matrix[(row * 4) + column] *= scale;
        }
    }

    private static void SubtractScaledRow(
        Span<double> matrix,
        int targetRow,
        int sourceRow,
        double scale) {
        for (var column = 0; column < 4; column++) {
            matrix[(targetRow * 4) + column] -=
                scale * matrix[(sourceRow * 4) + column];
        }
    }

}
