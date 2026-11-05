// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-component vector over a generic numeric scalar type.
/// </summary>
/// <remarks>
/// This type provides allocation-free arithmetic that is valid for every
/// <see cref="INumber{T}"/> scalar, including integers, rationals, and IEEE
/// floating-point types. Operations that require roots or transcendental
/// functions are intentionally not assumed here because those capabilities are
/// not part of <see cref="INumber{T}"/>.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fixed-size generic math types require operators and " +
        "static numeric primitives.")]
public readonly record struct Vector2<T>(T X, T Y) :
    IAdditionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    ISubtractionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IUnaryNegationOperators<Vector2<T>, Vector2<T>>,
    IMultiplyOperators<Vector2<T>, T, Vector2<T>>,
    IDivisionOperators<Vector2<T>, T, Vector2<T>>,
    IAdditiveIdentity<Vector2<T>, Vector2<T>>
    where T : INumber<T> {
    public static Vector2<T> Zero => default;

    public static Vector2<T> One => new(T.One, T.One);

    public static Vector2<T> UnitX => new(T.One, T.Zero);

    public static Vector2<T> UnitY => new(T.Zero, T.One);

    public static Vector2<T> AdditiveIdentity => Zero;

    public T LengthSquared => (X * X) + (Y * Y);

    public static T Dot(Vector2<T> left, Vector2<T> right) {
        return (left.X * right.X) + (left.Y * right.Y);
    }

    public static T Cross(Vector2<T> left, Vector2<T> right) {
        return (left.X * right.Y) - (left.Y * right.X);
    }

    public static T DistanceSquared(Vector2<T> left, Vector2<T> right) {
        return (left - right).LengthSquared;
    }

    public static Vector2<T> Perpendicular(Vector2<T> value) {
        return new Vector2<T>(-value.Y, value.X);
    }

    public static Vector2<T> Lerp(
        Vector2<T> start,
        Vector2<T> end,
        T amount) {
        return start + ((end - start) * amount);
    }

    public static Vector2<T> Add(Vector2<T> left, Vector2<T> right) {
        return left + right;
    }

    public static Vector2<T> Subtract(Vector2<T> left, Vector2<T> right) {
        return left - right;
    }

    public static Vector2<T> Negate(Vector2<T> value) {
        return -value;
    }

    public static Vector2<T> Multiply(Vector2<T> value, T scale) {
        return value * scale;
    }

    public static Vector2<T> Multiply(T scale, Vector2<T> value) {
        return value * scale;
    }

    public static Vector2<T> Divide(Vector2<T> value, T divisor) {
        return value / divisor;
    }

    public static Vector2<T> operator +(
        Vector2<T> left,
        Vector2<T> right) {
        return new Vector2<T>(left.X + right.X, left.Y + right.Y);
    }

    public static Vector2<T> operator -(
        Vector2<T> left,
        Vector2<T> right) {
        return new Vector2<T>(left.X - right.X, left.Y - right.Y);
    }

    public static Vector2<T> operator -(Vector2<T> value) {
        return new Vector2<T>(-value.X, -value.Y);
    }

    public static Vector2<T> operator *(Vector2<T> value, T scale) {
        return new Vector2<T>(value.X * scale, value.Y * scale);
    }

    public static Vector2<T> operator *(T scale, Vector2<T> value) {
        return value * scale;
    }

    public static Vector2<T> operator /(Vector2<T> value, T divisor) {
        return new Vector2<T>(value.X / divisor, value.Y / divisor);
    }
}
