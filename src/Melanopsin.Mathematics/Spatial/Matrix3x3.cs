// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-by-three matrix using row-major storage and column-vector
/// multiplication.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Matrix3x3 :
    IMultiplyOperators<Matrix3x3, Matrix3x3, Matrix3x3>,
    IMultiplyOperators<Matrix3x3, float, Matrix3x3>,
    IDivisionOperators<Matrix3x3, float, Matrix3x3>,
    IMultiplicativeIdentity<Matrix3x3, Matrix3x3>,
    IEqualityOperators<Matrix3x3, Matrix3x3, bool> {
    public Matrix3x3(
        float m11,
        float m12,
        float m13,
        float m21,
        float m22,
        float m23,
        float m31,
        float m32,
        float m33) {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M31 = m31;
        M32 = m32;
        M33 = m33;
    }

    public static Matrix3x3 Identity => new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);

    public static Matrix3x3 MultiplicativeIdentity => Identity;

    public static Matrix3x3 FromStorage(
        ReadOnlySpan<float> source,
        MatrixStorageOrder storageOrder) {
        if (source.Length < 9) {
            throw new ArgumentException(
                "A 3x3 matrix requires nine values.",
                nameof(source));
        }

        return storageOrder switch {
            MatrixStorageOrder.RowMajor => new Matrix3x3(
                source[0], source[1], source[2],
                source[3], source[4], source[5],
                source[6], source[7], source[8]),
            MatrixStorageOrder.ColumnMajor => new Matrix3x3(
                source[0], source[3], source[6],
                source[1], source[4], source[7],
                source[2], source[5], source[8]),
            _ => throw new ArgumentOutOfRangeException(nameof(storageOrder)),
        };
    }

    public float M11 { get; }

    public float M12 { get; }

    public float M13 { get; }

    public float M21 { get; }

    public float M22 { get; }

    public float M23 { get; }

    public float M31 { get; }

    public float M32 { get; }

    public float M33 { get; }

    public Vector3 Row1 => new(M11, M12, M13);

    public Vector3 Row2 => new(M21, M22, M23);

    public Vector3 Row3 => new(M31, M32, M33);

    /// <summary>
    /// Gets the determinant using double intermediates before rounding to
    /// <see cref="float"/>.
    /// </summary>
    public float Determinant {
        get {
            var m11 = (double)M11;
            var m12 = (double)M12;
            var m13 = (double)M13;
            var m21 = (double)M21;
            var m22 = (double)M22;
            var m23 = (double)M23;
            var m31 = (double)M31;
            var m32 = (double)M32;
            var m33 = (double)M33;
            return (float)(
                (m11 * ((m22 * m33) - (m23 * m32))) -
                (m12 * ((m21 * m33) - (m23 * m31))) +
                (m13 * ((m21 * m32) - (m22 * m31))));
        }
    }

    public static Matrix3x3 CreateRotationX(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            1, 0, 0,
            0, cosine, -sine,
            0, sine, cosine);
    }

    public static Matrix3x3 CreateRotationY(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            cosine, 0, sine,
            0, 1, 0,
            -sine, 0, cosine);
    }

    public static Matrix3x3 CreateRotationZ(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            cosine, -sine, 0,
            sine, cosine, 0,
            0, 0, 1);
    }

    public static Matrix3x3 CreateFromQuaternion(Quaternion quaternion) {
        if (!TryCreateFromQuaternion(
                quaternion,
                out var result,
                out var report)) {
            throw new ArithmeticException(
                report.Status == SpatialOperationStatus.NonFiniteInput
                    ? "A non-finite quaternion cannot define a rotation."
                    : "A zero quaternion cannot define a rotation.");
        }

        return result;
    }

    private static Matrix3x3 CreateFromNormalizedQuaternion(
        Quaternion quaternionValue) {
        var x = quaternionValue.X;
        var y = quaternionValue.Y;
        var z = quaternionValue.Z;
        var w = quaternionValue.W;
        var xx = x * x;
        var yy = y * y;
        var zz = z * z;
        var xy = x * y;
        var xz = x * z;
        var yz = y * z;
        var wx = w * x;
        var wy = w * y;
        var wz = w * z;

        return new Matrix3x3(
            1 - (2 * (yy + zz)), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - (2 * (xx + zz)), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - (2 * (xx + yy)));
    }

    public Vector3 Transform(Vector3 value) {
        return new Vector3(
            (float)(((double)M11 * value.X) + ((double)M12 * value.Y) + ((double)M13 * value.Z)),
            (float)(((double)M21 * value.X) + ((double)M22 * value.Y) + ((double)M23 * value.Z)),
            (float)(((double)M31 * value.X) + ((double)M32 * value.Y) + ((double)M33 * value.Z)));
    }

    public void CopyTo(
        Span<float> destination,
        MatrixStorageOrder storageOrder) {
        if (destination.Length < 9) {
            throw new ArgumentException(
                "A 3x3 matrix requires nine destination values.",
                nameof(destination));
        }

        switch (storageOrder) {
            case MatrixStorageOrder.RowMajor:
                destination[0] = M11;
                destination[1] = M12;
                destination[2] = M13;
                destination[3] = M21;
                destination[4] = M22;
                destination[5] = M23;
                destination[6] = M31;
                destination[7] = M32;
                destination[8] = M33;
                break;
            case MatrixStorageOrder.ColumnMajor:
                destination[0] = M11;
                destination[1] = M21;
                destination[2] = M31;
                destination[3] = M12;
                destination[4] = M22;
                destination[5] = M32;
                destination[6] = M13;
                destination[7] = M23;
                destination[8] = M33;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storageOrder));
        }
    }

    public Matrix3x3 Transpose() {
        return new Matrix3x3(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33);
    }

    public Matrix3x3 Invert() {
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
    public bool TryInvert(out Matrix3x3 inverse) {
        return TryInvert(out inverse, out _);
    }

    private bool TryInvertCore(out Matrix3x3 inverse) {
        double m11 = M11;
        double m12 = M12;
        double m13 = M13;
        double m21 = M21;
        double m22 = M22;
        double m23 = M23;
        double m31 = M31;
        double m32 = M32;
        double m33 = M33;
        var determinant =
            (m11 * ((m22 * m33) - (m23 * m32))) -
            (m12 * ((m21 * m33) - (m23 * m31))) +
            (m13 * ((m21 * m32) - (m22 * m31)));
        if (!double.IsFinite(determinant) || determinant == 0) {
            inverse = default;
            return false;
        }

        var inverseDeterminant = 1 / determinant;
        var i11 = ((m22 * m33) - (m23 * m32)) * inverseDeterminant;
        var i12 = ((m13 * m32) - (m12 * m33)) * inverseDeterminant;
        var i13 = ((m12 * m23) - (m13 * m22)) * inverseDeterminant;
        var i21 = ((m23 * m31) - (m21 * m33)) * inverseDeterminant;
        var i22 = ((m11 * m33) - (m13 * m31)) * inverseDeterminant;
        var i23 = ((m13 * m21) - (m11 * m23)) * inverseDeterminant;
        var i31 = ((m21 * m32) - (m22 * m31)) * inverseDeterminant;
        var i32 = ((m12 * m31) - (m11 * m32)) * inverseDeterminant;
        var i33 = ((m11 * m22) - (m12 * m21)) * inverseDeterminant;
        if (!MatrixNumerics.IsRepresentableAsFloat(i11) ||
            !MatrixNumerics.IsRepresentableAsFloat(i12) ||
            !MatrixNumerics.IsRepresentableAsFloat(i13) ||
            !MatrixNumerics.IsRepresentableAsFloat(i21) ||
            !MatrixNumerics.IsRepresentableAsFloat(i22) ||
            !MatrixNumerics.IsRepresentableAsFloat(i23) ||
            !MatrixNumerics.IsRepresentableAsFloat(i31) ||
            !MatrixNumerics.IsRepresentableAsFloat(i32) ||
            !MatrixNumerics.IsRepresentableAsFloat(i33)) {
            inverse = default;
            return false;
        }

        inverse = new Matrix3x3(
            (float)i11,
            (float)i12,
            (float)i13,
            (float)i21,
            (float)i22,
            (float)i23,
            (float)i31,
            (float)i32,
            (float)i33);
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

    public static Matrix3x3 operator *(
        Matrix3x3 left,
        Matrix3x3 right) {
        return Multiply(left, right);
    }

    public static Matrix3x3 Multiply(Matrix3x3 left, Matrix3x3 right) {
        return new Matrix3x3(
            (float)(((double)left.M11 * right.M11) + ((double)left.M12 * right.M21) + ((double)left.M13 * right.M31)),
            (float)(((double)left.M11 * right.M12) + ((double)left.M12 * right.M22) + ((double)left.M13 * right.M32)),
            (float)(((double)left.M11 * right.M13) + ((double)left.M12 * right.M23) + ((double)left.M13 * right.M33)),
            (float)(((double)left.M21 * right.M11) + ((double)left.M22 * right.M21) + ((double)left.M23 * right.M31)),
            (float)(((double)left.M21 * right.M12) + ((double)left.M22 * right.M22) + ((double)left.M23 * right.M32)),
            (float)(((double)left.M21 * right.M13) + ((double)left.M22 * right.M23) + ((double)left.M23 * right.M33)),
            (float)(((double)left.M31 * right.M11) + ((double)left.M32 * right.M21) + ((double)left.M33 * right.M31)),
            (float)(((double)left.M31 * right.M12) + ((double)left.M32 * right.M22) + ((double)left.M33 * right.M32)),
            (float)(((double)left.M31 * right.M13) + ((double)left.M32 * right.M23) + ((double)left.M33 * right.M33)));
    }

    public static Vector3 Multiply(Matrix3x3 matrix, Vector3 value) {
        return matrix.Transform(value);
    }

    public static Matrix3x3 Multiply(Matrix3x3 matrix, float scale) {
        return new Matrix3x3(
            matrix.M11 * scale, matrix.M12 * scale, matrix.M13 * scale,
            matrix.M21 * scale, matrix.M22 * scale, matrix.M23 * scale,
            matrix.M31 * scale, matrix.M32 * scale, matrix.M33 * scale);
    }

    public static Matrix3x3 Multiply(float scale, Matrix3x3 matrix) {
        return Multiply(matrix, scale);
    }

    public static Matrix3x3 Divide(Matrix3x3 matrix, float divisor) {
        return new Matrix3x3(
            matrix.M11 / divisor, matrix.M12 / divisor, matrix.M13 / divisor,
            matrix.M21 / divisor, matrix.M22 / divisor, matrix.M23 / divisor,
            matrix.M31 / divisor, matrix.M32 / divisor, matrix.M33 / divisor);
    }

    public static Vector3 operator *(Matrix3x3 matrix, Vector3 value) {
        return Multiply(matrix, value);
    }

    public static Matrix3x3 operator *(Matrix3x3 matrix, float scale) {
        return Multiply(matrix, scale);
    }

    public static Matrix3x3 operator *(float scale, Matrix3x3 matrix) {
        return Multiply(scale, matrix);
    }

    public static Matrix3x3 operator /(Matrix3x3 matrix, float divisor) {
        return Divide(matrix, divisor);
    }
}
