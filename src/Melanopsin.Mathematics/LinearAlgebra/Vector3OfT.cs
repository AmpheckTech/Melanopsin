using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-component vector over a generic numeric scalar type.
/// </summary>
/// <remarks>
/// Arithmetic remains allocation-free and valid for any
/// <see cref="INumber{T}"/>. Root-based normalization is outside this type's
/// contract because <see cref="INumber{T}"/> does not imply root support.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Vector3<T>(T X, T Y, T Z) :
    IAdditionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    ISubtractionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IUnaryNegationOperators<Vector3<T>, Vector3<T>>,
    IMultiplyOperators<Vector3<T>, T, Vector3<T>>,
    IDivisionOperators<Vector3<T>, T, Vector3<T>>,
    IAdditiveIdentity<Vector3<T>, Vector3<T>>
    where T : INumber<T> {
    public static Vector3<T> Zero => default;

    public static Vector3<T> One => new(T.One, T.One, T.One);

    public static Vector3<T> UnitX => new(T.One, T.Zero, T.Zero);

    public static Vector3<T> UnitY => new(T.Zero, T.One, T.Zero);

    public static Vector3<T> UnitZ => new(T.Zero, T.Zero, T.One);

    public static Vector3<T> AdditiveIdentity => Zero;

    public T LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public static T Dot(Vector3<T> left, Vector3<T> right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z);
    }

    public static Vector3<T> Cross(Vector3<T> left, Vector3<T> right) {
        return new Vector3<T>(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));
    }

    public static T DistanceSquared(Vector3<T> left, Vector3<T> right) {
        return (left - right).LengthSquared;
    }

    public static Vector3<T> Lerp(
        Vector3<T> start,
        Vector3<T> end,
        T amount) {
        return start + ((end - start) * amount);
    }

    public static Vector3<T> Add(Vector3<T> left, Vector3<T> right) {
        return left + right;
    }

    public static Vector3<T> Subtract(Vector3<T> left, Vector3<T> right) {
        return left - right;
    }

    public static Vector3<T> Negate(Vector3<T> value) {
        return -value;
    }

    public static Vector3<T> Multiply(Vector3<T> value, T scale) {
        return value * scale;
    }

    public static Vector3<T> Multiply(T scale, Vector3<T> value) {
        return value * scale;
    }

    public static Vector3<T> Divide(Vector3<T> value, T divisor) {
        return value / divisor;
    }

    public static Vector3<T> operator +(
        Vector3<T> left,
        Vector3<T> right) {
        return new Vector3<T>(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    public static Vector3<T> operator -(
        Vector3<T> left,
        Vector3<T> right) {
        return new Vector3<T>(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }

    public static Vector3<T> operator -(Vector3<T> value) {
        return new Vector3<T>(-value.X, -value.Y, -value.Z);
    }

    public static Vector3<T> operator *(Vector3<T> value, T scale) {
        return new Vector3<T>(
            value.X * scale,
            value.Y * scale,
            value.Z * scale);
    }

    public static Vector3<T> operator *(T scale, Vector3<T> value) {
        return value * scale;
    }

    public static Vector3<T> operator /(Vector3<T> value, T divisor) {
        return new Vector3<T>(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor);
    }
}
