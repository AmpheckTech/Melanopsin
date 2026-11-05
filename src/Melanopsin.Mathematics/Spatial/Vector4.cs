// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A homogeneous four-component vector used by matrix transforms.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Vector4 :
    IAdditionOperators<Vector4, Vector4, Vector4>,
    ISubtractionOperators<Vector4, Vector4, Vector4>,
    IUnaryNegationOperators<Vector4, Vector4>,
    IMultiplyOperators<Vector4, float, Vector4>,
    IDivisionOperators<Vector4, float, Vector4>,
    IAdditiveIdentity<Vector4, Vector4>,
    IEqualityOperators<Vector4, Vector4, bool> {
    public Vector4(float x, float y, float z, float w) {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(Vector3 vector, float w) {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
        W = w;
    }

    public static Vector4 Zero => default;

    public static Vector4 One => new(1, 1, 1, 1);

    public static Vector4 UnitX => new(1, 0, 0, 0);

    public static Vector4 UnitY => new(0, 1, 0, 0);

    public static Vector4 UnitZ => new(0, 0, 1, 0);

    public static Vector4 UnitW => new(0, 0, 0, 1);

    public static Vector4 AdditiveIdentity => Zero;

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public float W { get; }

    /// <summary>
    /// Gets the squared magnitude. The sum of squares is formed in double
    /// precision, then rounded to <see cref="float"/>; a mathematical value
    /// above <see cref="float.MaxValue"/> therefore returns positive infinity.
    /// </summary>
    public float LengthSquared => (float)(
        ((double)X * X) +
        ((double)Y * Y) +
        ((double)Z * Z) +
        ((double)W * W));

    /// <summary>
    /// Gets the magnitude without an avoidable intermediate squaring overflow.
    /// </summary>
    public float Length => (float)Math.Sqrt(
        ((double)X * X) +
        ((double)Y * Y) +
        ((double)Z * Z) +
        ((double)W * W));

    public Vector3 XYZ => new(X, Y, Z);

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z) &&
        float.IsFinite(W);

    public static float Dot(Vector4 left, Vector4 right) {
        return (float)(
            ((double)left.X * right.X) +
            ((double)left.Y * right.Y) +
            ((double)left.Z * right.Z) +
            ((double)left.W * right.W));
    }

    public Vector4 Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out Vector4 result) {
        return TryNormalize(out result, out _);
    }

    public static Vector4 Add(Vector4 left, Vector4 right) {
        return new Vector4(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);
    }

    public static Vector4 Subtract(Vector4 left, Vector4 right) {
        return new Vector4(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);
    }

    public static Vector4 Negate(Vector4 value) {
        return new Vector4(-value.X, -value.Y, -value.Z, -value.W);
    }

    public static Vector4 Multiply(Vector4 value, float scale) {
        return new Vector4(
            value.X * scale,
            value.Y * scale,
            value.Z * scale,
            value.W * scale);
    }

    public static Vector4 Multiply(float scale, Vector4 value) {
        return Multiply(value, scale);
    }

    public static Vector4 Divide(Vector4 value, float divisor) {
        return new Vector4(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor,
            value.W / divisor);
    }

    public static Vector4 operator +(Vector4 left, Vector4 right) {
        return Add(left, right);
    }

    public static Vector4 operator -(Vector4 left, Vector4 right) {
        return Subtract(left, right);
    }

    public static Vector4 operator -(Vector4 value) {
        return Negate(value);
    }

    public static Vector4 operator *(Vector4 value, float scale) {
        return Multiply(value, scale);
    }

    public static Vector4 operator *(float scale, Vector4 value) {
        return Multiply(scale, value);
    }

    public static Vector4 operator /(Vector4 value, float divisor) {
        return Divide(value, divisor);
    }
}
