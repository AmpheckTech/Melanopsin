using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A homogeneous four-component vector used by matrix transforms.
/// </summary>
public readonly record struct Vector4 :
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

    public static Vector4 AdditiveIdentity => Zero;

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public float W { get; }

    public float LengthSquared =>
        (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public float Length => MathF.Sqrt(LengthSquared);

    public Vector3 XYZ => new(X, Y, Z);

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z) &&
        float.IsFinite(W);

    public static float Dot(Vector4 left, Vector4 right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z) +
            (left.W * right.W);
    }

    public Vector4 Normalize() {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            throw new InvalidOperationException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return this / MathF.Sqrt(lengthSquared);
    }

    public static Vector4 operator +(Vector4 left, Vector4 right) {
        return new Vector4(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);
    }

    public static Vector4 operator -(Vector4 left, Vector4 right) {
        return new Vector4(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);
    }

    public static Vector4 operator -(Vector4 value) {
        return new Vector4(-value.X, -value.Y, -value.Z, -value.W);
    }

    public static Vector4 operator *(Vector4 value, float scale) {
        return new Vector4(
            value.X * scale,
            value.Y * scale,
            value.Z * scale,
            value.W * scale);
    }

    public static Vector4 operator *(float scale, Vector4 value) {
        return value * scale;
    }

    public static Vector4 operator /(Vector4 value, float divisor) {
        return new Vector4(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor,
            value.W / divisor);
    }
}
