using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-by-three row-major matrix over a generic numeric scalar type.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Matrix3x3<T>(
    T M11,
    T M12,
    T M13,
    T M21,
    T M22,
    T M23,
    T M31,
    T M32,
    T M33) :
    IAdditionOperators<Matrix3x3<T>, Matrix3x3<T>, Matrix3x3<T>>,
    ISubtractionOperators<Matrix3x3<T>, Matrix3x3<T>, Matrix3x3<T>>,
    IUnaryNegationOperators<Matrix3x3<T>, Matrix3x3<T>>,
    IMultiplyOperators<Matrix3x3<T>, Matrix3x3<T>, Matrix3x3<T>>,
    IMultiplyOperators<Matrix3x3<T>, T, Matrix3x3<T>>,
    IDivisionOperators<Matrix3x3<T>, T, Matrix3x3<T>>,
    IAdditiveIdentity<Matrix3x3<T>, Matrix3x3<T>>,
    IMultiplicativeIdentity<Matrix3x3<T>, Matrix3x3<T>>
    where T : INumber<T> {
    public static Matrix3x3<T> Zero => default;

    public static Matrix3x3<T> Identity => new(
        T.One, T.Zero, T.Zero,
        T.Zero, T.One, T.Zero,
        T.Zero, T.Zero, T.One);

    public static Matrix3x3<T> AdditiveIdentity => Zero;

    public static Matrix3x3<T> MultiplicativeIdentity => Identity;

    public Vector3<T> Row1 => new(M11, M12, M13);

    public Vector3<T> Row2 => new(M21, M22, M23);

    public Vector3<T> Row3 => new(M31, M32, M33);

    public Vector3<T> Column1 => new(M11, M21, M31);

    public Vector3<T> Column2 => new(M12, M22, M32);

    public Vector3<T> Column3 => new(M13, M23, M33);

    public T Trace => M11 + M22 + M33;

    public T Determinant =>
        (M11 * ((M22 * M33) - (M23 * M32))) -
        (M12 * ((M21 * M33) - (M23 * M31))) +
        (M13 * ((M21 * M32) - (M22 * M31)));

    public Vector3<T> Transform(Vector3<T> value) {
        return new Vector3<T>(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z),
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z),
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z));
    }

    public Matrix3x3<T> Transpose() {
        return new Matrix3x3<T>(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33);
    }

    public static Matrix3x3<T> Add(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return left + right;
    }

    public static Matrix3x3<T> Subtract(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return left - right;
    }

    public static Matrix3x3<T> Negate(Matrix3x3<T> value) {
        return -value;
    }

    public static Matrix3x3<T> Multiply(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return left * right;
    }

    public static Vector3<T> Multiply(
        Matrix3x3<T> matrix,
        Vector3<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix3x3<T> Multiply(Matrix3x3<T> matrix, T scale) {
        return matrix * scale;
    }

    public static Matrix3x3<T> Multiply(T scale, Matrix3x3<T> matrix) {
        return matrix * scale;
    }

    public static Matrix3x3<T> Divide(Matrix3x3<T> matrix, T divisor) {
        return matrix / divisor;
    }

    public static Matrix3x3<T> operator +(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return new Matrix3x3<T>(
            left.M11 + right.M11,
            left.M12 + right.M12,
            left.M13 + right.M13,
            left.M21 + right.M21,
            left.M22 + right.M22,
            left.M23 + right.M23,
            left.M31 + right.M31,
            left.M32 + right.M32,
            left.M33 + right.M33);
    }

    public static Matrix3x3<T> operator -(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return new Matrix3x3<T>(
            left.M11 - right.M11,
            left.M12 - right.M12,
            left.M13 - right.M13,
            left.M21 - right.M21,
            left.M22 - right.M22,
            left.M23 - right.M23,
            left.M31 - right.M31,
            left.M32 - right.M32,
            left.M33 - right.M33);
    }

    public static Matrix3x3<T> operator -(Matrix3x3<T> value) {
        return new Matrix3x3<T>(
            -value.M11,
            -value.M12,
            -value.M13,
            -value.M21,
            -value.M22,
            -value.M23,
            -value.M31,
            -value.M32,
            -value.M33);
    }

    public static Matrix3x3<T> operator *(
        Matrix3x3<T> left,
        Matrix3x3<T> right) {
        return new Matrix3x3<T>(
            (left.M11 * right.M11) +
                (left.M12 * right.M21) +
                (left.M13 * right.M31),
            (left.M11 * right.M12) +
                (left.M12 * right.M22) +
                (left.M13 * right.M32),
            (left.M11 * right.M13) +
                (left.M12 * right.M23) +
                (left.M13 * right.M33),
            (left.M21 * right.M11) +
                (left.M22 * right.M21) +
                (left.M23 * right.M31),
            (left.M21 * right.M12) +
                (left.M22 * right.M22) +
                (left.M23 * right.M32),
            (left.M21 * right.M13) +
                (left.M22 * right.M23) +
                (left.M23 * right.M33),
            (left.M31 * right.M11) +
                (left.M32 * right.M21) +
                (left.M33 * right.M31),
            (left.M31 * right.M12) +
                (left.M32 * right.M22) +
                (left.M33 * right.M32),
            (left.M31 * right.M13) +
                (left.M32 * right.M23) +
                (left.M33 * right.M33));
    }

    public static Vector3<T> operator *(
        Matrix3x3<T> matrix,
        Vector3<T> value) {
        return matrix.Transform(value);
    }

    public static Matrix3x3<T> operator *(
        Matrix3x3<T> matrix,
        T scale) {
        return new Matrix3x3<T>(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M13 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale,
            matrix.M23 * scale,
            matrix.M31 * scale,
            matrix.M32 * scale,
            matrix.M33 * scale);
    }

    public static Matrix3x3<T> operator *(
        T scale,
        Matrix3x3<T> matrix) {
        return matrix * scale;
    }

    public static Matrix3x3<T> operator /(
        Matrix3x3<T> matrix,
        T divisor) {
        return new Matrix3x3<T>(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M13 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor,
            matrix.M23 / divisor,
            matrix.M31 / divisor,
            matrix.M32 / divisor,
            matrix.M33 / divisor);
    }
}
