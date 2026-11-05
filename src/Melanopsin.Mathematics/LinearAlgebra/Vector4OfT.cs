using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A four-component vector over a generic numeric scalar type.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Vector4<T>(T X, T Y, T Z, T W) :
    IAdditionOperators<Vector4<T>, Vector4<T>, Vector4<T>>,
    ISubtractionOperators<Vector4<T>, Vector4<T>, Vector4<T>>,
    IUnaryNegationOperators<Vector4<T>, Vector4<T>>,
    IMultiplyOperators<Vector4<T>, T, Vector4<T>>,
    IDivisionOperators<Vector4<T>, T, Vector4<T>>,
    IAdditiveIdentity<Vector4<T>, Vector4<T>>
    where T : INumber<T> {
    public static Vector4<T> Zero => default;

    public static Vector4<T> One => new(T.One, T.One, T.One, T.One);

    public static Vector4<T> UnitX => new(T.One, T.Zero, T.Zero, T.Zero);

    public static Vector4<T> UnitY => new(T.Zero, T.One, T.Zero, T.Zero);

    public static Vector4<T> UnitZ => new(T.Zero, T.Zero, T.One, T.Zero);

    public static Vector4<T> UnitW => new(T.Zero, T.Zero, T.Zero, T.One);

    public static Vector4<T> AdditiveIdentity => Zero;

    public T LengthSquared => (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public static T Dot(Vector4<T> left, Vector4<T> right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z) +
            (left.W * right.W);
    }

    public static T DistanceSquared(Vector4<T> left, Vector4<T> right) {
        return (left - right).LengthSquared;
    }

    public static Vector4<T> Lerp(
        Vector4<T> start,
        Vector4<T> end,
        T amount) {
        return start + ((end - start) * amount);
    }

    public static Vector4<T> Add(Vector4<T> left, Vector4<T> right) {
        return left + right;
    }

    public static Vector4<T> Subtract(Vector4<T> left, Vector4<T> right) {
        return left - right;
    }

    public static Vector4<T> Negate(Vector4<T> value) {
        return -value;
    }

    public static Vector4<T> Multiply(Vector4<T> value, T scale) {
        return value * scale;
    }

    public static Vector4<T> Multiply(T scale, Vector4<T> value) {
        return value * scale;
    }

    public static Vector4<T> Divide(Vector4<T> value, T divisor) {
        return value / divisor;
    }

    public static Vector4<T> operator +(
        Vector4<T> left,
        Vector4<T> right) {
        return new Vector4<T>(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);
    }

    public static Vector4<T> operator -(
        Vector4<T> left,
        Vector4<T> right) {
        return new Vector4<T>(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);
    }

    public static Vector4<T> operator -(Vector4<T> value) {
        return new Vector4<T>(-value.X, -value.Y, -value.Z, -value.W);
    }

    public static Vector4<T> operator *(Vector4<T> value, T scale) {
        return new Vector4<T>(
            value.X * scale,
            value.Y * scale,
            value.Z * scale,
            value.W * scale);
    }

    public static Vector4<T> operator *(T scale, Vector4<T> value) {
        return value * scale;
    }

    public static Vector4<T> operator /(Vector4<T> value, T divisor) {
        return new Vector4<T>(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor,
            value.W / divisor);
    }
}
