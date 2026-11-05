// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A four-by-four row-major matrix over a generic numeric scalar type.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Matrix4x4<T>(
    T M11,
    T M12,
    T M13,
    T M14,
    T M21,
    T M22,
    T M23,
    T M24,
    T M31,
    T M32,
    T M33,
    T M34,
    T M41,
    T M42,
    T M43,
    T M44) :
    IAdditionOperators<Matrix4x4<T>, Matrix4x4<T>, Matrix4x4<T>>,
    ISubtractionOperators<Matrix4x4<T>, Matrix4x4<T>, Matrix4x4<T>>,
    IUnaryNegationOperators<Matrix4x4<T>, Matrix4x4<T>>,
    IMultiplyOperators<Matrix4x4<T>, Matrix4x4<T>, Matrix4x4<T>>,
    IMultiplyOperators<Matrix4x4<T>, T, Matrix4x4<T>>,
    IDivisionOperators<Matrix4x4<T>, T, Matrix4x4<T>>,
    IAdditiveIdentity<Matrix4x4<T>, Matrix4x4<T>>,
    IMultiplicativeIdentity<Matrix4x4<T>, Matrix4x4<T>>
    where T : INumber<T> {
    public static Matrix4x4<T> Zero => default;

    public static Matrix4x4<T> Identity => new(
        T.One, T.Zero, T.Zero, T.Zero,
        T.Zero, T.One, T.Zero, T.Zero,
        T.Zero, T.Zero, T.One, T.Zero,
        T.Zero, T.Zero, T.Zero, T.One);

    public static Matrix4x4<T> AdditiveIdentity => Zero;

    public static Matrix4x4<T> MultiplicativeIdentity => Identity;

    public Vector4<T> Row1 => new(M11, M12, M13, M14);

    public Vector4<T> Row2 => new(M21, M22, M23, M24);

    public Vector4<T> Row3 => new(M31, M32, M33, M34);

    public Vector4<T> Row4 => new(M41, M42, M43, M44);

    public Vector4<T> Column1 => new(M11, M21, M31, M41);

    public Vector4<T> Column2 => new(M12, M22, M32, M42);

    public Vector4<T> Column3 => new(M13, M23, M33, M43);

    public Vector4<T> Column4 => new(M14, M24, M34, M44);

    public T Trace => M11 + M22 + M33 + M44;

    public T Determinant =>
        (M11 * Determinant3(
            M22, M23, M24,
            M32, M33, M34,
            M42, M43, M44)) -
        (M12 * Determinant3(
            M21, M23, M24,
            M31, M33, M34,
            M41, M43, M44)) +
        (M13 * Determinant3(
            M21, M22, M24,
            M31, M32, M34,
            M41, M42, M44)) -
        (M14 * Determinant3(
            M21, M22, M23,
            M31, M32, M33,
            M41, M42, M43));

    public Vector4<T> Transform(Vector4<T> value) {
        return new Vector4<T>(
            (M11 * value.X) + (M12 * value.Y) +
                (M13 * value.Z) + (M14 * value.W),
            (M21 * value.X) + (M22 * value.Y) +
                (M23 * value.Z) + (M24 * value.W),
            (M31 * value.X) + (M32 * value.Y) +
                (M33 * value.Z) + (M34 * value.W),
            (M41 * value.X) + (M42 * value.Y) +
                (M43 * value.Z) + (M44 * value.W));
    }

    public Matrix4x4<T> Transpose() {
        return new Matrix4x4<T>(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44);
    }

    public static Matrix4x4<T> Add(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return left + right;
    }

    public static Matrix4x4<T> Subtract(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return left - right;
    }

    public static Matrix4x4<T> Negate(Matrix4x4<T> value) {
        return -value;
    }

    public static Matrix4x4<T> Multiply(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return left * right;
    }

    public static Vector4<T> Multiply(
        Matrix4x4<T> matrix,
        Vector4<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix4x4<T> Multiply(Matrix4x4<T> matrix, T scale) {
        return matrix * scale;
    }

    public static Matrix4x4<T> Multiply(T scale, Matrix4x4<T> matrix) {
        return matrix * scale;
    }

    public static Matrix4x4<T> Divide(Matrix4x4<T> matrix, T divisor) {
        return matrix / divisor;
    }

    public static Matrix4x4<T> operator +(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return new Matrix4x4<T>(
            left.M11 + right.M11,
            left.M12 + right.M12,
            left.M13 + right.M13,
            left.M14 + right.M14,
            left.M21 + right.M21,
            left.M22 + right.M22,
            left.M23 + right.M23,
            left.M24 + right.M24,
            left.M31 + right.M31,
            left.M32 + right.M32,
            left.M33 + right.M33,
            left.M34 + right.M34,
            left.M41 + right.M41,
            left.M42 + right.M42,
            left.M43 + right.M43,
            left.M44 + right.M44);
    }

    public static Matrix4x4<T> operator -(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return new Matrix4x4<T>(
            left.M11 - right.M11,
            left.M12 - right.M12,
            left.M13 - right.M13,
            left.M14 - right.M14,
            left.M21 - right.M21,
            left.M22 - right.M22,
            left.M23 - right.M23,
            left.M24 - right.M24,
            left.M31 - right.M31,
            left.M32 - right.M32,
            left.M33 - right.M33,
            left.M34 - right.M34,
            left.M41 - right.M41,
            left.M42 - right.M42,
            left.M43 - right.M43,
            left.M44 - right.M44);
    }

    public static Matrix4x4<T> operator -(Matrix4x4<T> value) {
        return new Matrix4x4<T>(
            -value.M11,
            -value.M12,
            -value.M13,
            -value.M14,
            -value.M21,
            -value.M22,
            -value.M23,
            -value.M24,
            -value.M31,
            -value.M32,
            -value.M33,
            -value.M34,
            -value.M41,
            -value.M42,
            -value.M43,
            -value.M44);
    }

    public static Matrix4x4<T> operator *(
        Matrix4x4<T> left,
        Matrix4x4<T> right) {
        return new Matrix4x4<T>(
            Dot(left.M11, left.M12, left.M13, left.M14, right.M11, right.M21, right.M31, right.M41),
            Dot(left.M11, left.M12, left.M13, left.M14, right.M12, right.M22, right.M32, right.M42),
            Dot(left.M11, left.M12, left.M13, left.M14, right.M13, right.M23, right.M33, right.M43),
            Dot(left.M11, left.M12, left.M13, left.M14, right.M14, right.M24, right.M34, right.M44),
            Dot(left.M21, left.M22, left.M23, left.M24, right.M11, right.M21, right.M31, right.M41),
            Dot(left.M21, left.M22, left.M23, left.M24, right.M12, right.M22, right.M32, right.M42),
            Dot(left.M21, left.M22, left.M23, left.M24, right.M13, right.M23, right.M33, right.M43),
            Dot(left.M21, left.M22, left.M23, left.M24, right.M14, right.M24, right.M34, right.M44),
            Dot(left.M31, left.M32, left.M33, left.M34, right.M11, right.M21, right.M31, right.M41),
            Dot(left.M31, left.M32, left.M33, left.M34, right.M12, right.M22, right.M32, right.M42),
            Dot(left.M31, left.M32, left.M33, left.M34, right.M13, right.M23, right.M33, right.M43),
            Dot(left.M31, left.M32, left.M33, left.M34, right.M14, right.M24, right.M34, right.M44),
            Dot(left.M41, left.M42, left.M43, left.M44, right.M11, right.M21, right.M31, right.M41),
            Dot(left.M41, left.M42, left.M43, left.M44, right.M12, right.M22, right.M32, right.M42),
            Dot(left.M41, left.M42, left.M43, left.M44, right.M13, right.M23, right.M33, right.M43),
            Dot(left.M41, left.M42, left.M43, left.M44, right.M14, right.M24, right.M34, right.M44));
    }

    public static Vector4<T> operator *(
        Matrix4x4<T> matrix,
        Vector4<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix4x4<T> operator *(
        Matrix4x4<T> matrix,
        T scale) {
        return new Matrix4x4<T>(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M13 * scale,
            matrix.M14 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale,
            matrix.M23 * scale,
            matrix.M24 * scale,
            matrix.M31 * scale,
            matrix.M32 * scale,
            matrix.M33 * scale,
            matrix.M34 * scale,
            matrix.M41 * scale,
            matrix.M42 * scale,
            matrix.M43 * scale,
            matrix.M44 * scale);
    }

    public static Matrix4x4<T> operator *(
        T scale,
        Matrix4x4<T> matrix) {
        return matrix * scale;
    }

    public static Matrix4x4<T> operator /(
        Matrix4x4<T> matrix,
        T divisor) {
        return new Matrix4x4<T>(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M13 / divisor,
            matrix.M14 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor,
            matrix.M23 / divisor,
            matrix.M24 / divisor,
            matrix.M31 / divisor,
            matrix.M32 / divisor,
            matrix.M33 / divisor,
            matrix.M34 / divisor,
            matrix.M41 / divisor,
            matrix.M42 / divisor,
            matrix.M43 / divisor,
            matrix.M44 / divisor);
    }

    private static T Determinant3(
        T m11,
        T m12,
        T m13,
        T m21,
        T m22,
        T m23,
        T m31,
        T m32,
        T m33) {
        return (m11 * ((m22 * m33) - (m23 * m32))) -
            (m12 * ((m21 * m33) - (m23 * m31))) +
            (m13 * ((m21 * m32) - (m22 * m31)));
    }

    private static T Dot(
        T a1,
        T a2,
        T a3,
        T a4,
        T b1,
        T b2,
        T b3,
        T b4) {
        return (a1 * b1) + (a2 * b2) + (a3 * b3) + (a4 * b4);
    }
}
