// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-by-two row-major matrix over a generic numeric scalar type.
/// </summary>
/// <remarks>
/// Multiplication is column-vector oriented. Determinants and multiplication
/// require only <see cref="INumber{T}"/>. Division by a determinant is not
/// exposed as inversion because integer scalar types do not form a field.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Matrix2x2<T>(
    T M11,
    T M12,
    T M21,
    T M22) :
    IAdditionOperators<Matrix2x2<T>, Matrix2x2<T>, Matrix2x2<T>>,
    ISubtractionOperators<Matrix2x2<T>, Matrix2x2<T>, Matrix2x2<T>>,
    IUnaryNegationOperators<Matrix2x2<T>, Matrix2x2<T>>,
    IMultiplyOperators<Matrix2x2<T>, Matrix2x2<T>, Matrix2x2<T>>,
    IMultiplyOperators<Matrix2x2<T>, T, Matrix2x2<T>>,
    IDivisionOperators<Matrix2x2<T>, T, Matrix2x2<T>>,
    IAdditiveIdentity<Matrix2x2<T>, Matrix2x2<T>>,
    IMultiplicativeIdentity<Matrix2x2<T>, Matrix2x2<T>>
    where T : INumber<T> {
    public static Matrix2x2<T> Zero => default;

    public static Matrix2x2<T> Identity => new(
        T.One, T.Zero,
        T.Zero, T.One);

    public static Matrix2x2<T> AdditiveIdentity => Zero;

    public static Matrix2x2<T> MultiplicativeIdentity => Identity;

    public Vector2<T> Row1 => new(M11, M12);

    public Vector2<T> Row2 => new(M21, M22);

    public Vector2<T> Column1 => new(M11, M21);

    public Vector2<T> Column2 => new(M12, M22);

    public T Trace => M11 + M22;

    public T Determinant => (M11 * M22) - (M12 * M21);

    public Vector2<T> Transform(Vector2<T> value) {
        return new Vector2<T>(
            (M11 * value.X) + (M12 * value.Y),
            (M21 * value.X) + (M22 * value.Y));
    }

    public Matrix2x2<T> Transpose() {
        return new Matrix2x2<T>(M11, M21, M12, M22);
    }

    public static Matrix2x2<T> Add(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return left + right;
    }

    public static Matrix2x2<T> Subtract(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return left - right;
    }

    public static Matrix2x2<T> Negate(Matrix2x2<T> value) {
        return -value;
    }

    public static Matrix2x2<T> Multiply(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return left * right;
    }

    public static Vector2<T> Multiply(
        Matrix2x2<T> matrix,
        Vector2<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix2x2<T> Multiply(Matrix2x2<T> matrix, T scale) {
        return matrix * scale;
    }

    public static Matrix2x2<T> Multiply(T scale, Matrix2x2<T> matrix) {
        return matrix * scale;
    }

    public static Matrix2x2<T> Divide(Matrix2x2<T> matrix, T divisor) {
        return matrix / divisor;
    }

    public static Matrix2x2<T> operator +(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return new Matrix2x2<T>(
            left.M11 + right.M11,
            left.M12 + right.M12,
            left.M21 + right.M21,
            left.M22 + right.M22);
    }

    public static Matrix2x2<T> operator -(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return new Matrix2x2<T>(
            left.M11 - right.M11,
            left.M12 - right.M12,
            left.M21 - right.M21,
            left.M22 - right.M22);
    }

    public static Matrix2x2<T> operator -(Matrix2x2<T> value) {
        return new Matrix2x2<T>(
            -value.M11,
            -value.M12,
            -value.M21,
            -value.M22);
    }

    public static Matrix2x2<T> operator *(
        Matrix2x2<T> left,
        Matrix2x2<T> right) {
        return new Matrix2x2<T>(
            (left.M11 * right.M11) + (left.M12 * right.M21),
            (left.M11 * right.M12) + (left.M12 * right.M22),
            (left.M21 * right.M11) + (left.M22 * right.M21),
            (left.M21 * right.M12) + (left.M22 * right.M22));
    }

    public static Vector2<T> operator *(
        Matrix2x2<T> matrix,
        Vector2<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix2x2<T> operator *(
        Matrix2x2<T> matrix,
        T scale) {
        return new Matrix2x2<T>(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale);
    }

    public static Matrix2x2<T> operator *(
        T scale,
        Matrix2x2<T> matrix) {
        return matrix * scale;
    }

    public static Matrix2x2<T> operator /(
        Matrix2x2<T> matrix,
        T divisor) {
        return new Matrix2x2<T>(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor);
    }
}
