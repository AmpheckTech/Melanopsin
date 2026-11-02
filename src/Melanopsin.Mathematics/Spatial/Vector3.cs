using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A direction and magnitude in three-dimensional space.
/// </summary>
public readonly record struct Vector3 :
    IAdditionOperators<Vector3, Vector3, Vector3>,
    ISubtractionOperators<Vector3, Vector3, Vector3>,
    IUnaryNegationOperators<Vector3, Vector3>,
    IMultiplyOperators<Vector3, float, Vector3>,
    IDivisionOperators<Vector3, float, Vector3>,
    IAdditiveIdentity<Vector3, Vector3>,
    IEqualityOperators<Vector3, Vector3, bool> {
    public Vector3(float x, float y, float z) {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => default;

    public static Vector3 One => new(1, 1, 1);

    public static Vector3 UnitX => new(1, 0, 0);

    public static Vector3 UnitY => new(0, 1, 0);

    public static Vector3 UnitZ => new(0, 0, 1);

    public static Vector3 AdditiveIdentity => Zero;

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public float LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public float Length => MathF.Sqrt(LengthSquared);

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z);

    public static float Dot(Vector3 left, Vector3 right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z);
    }

    public static Vector3 Cross(Vector3 left, Vector3 right) {
        return new Vector3(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));
    }

    public static float DistanceSquared(Vector3 left, Vector3 right) {
        return (left - right).LengthSquared;
    }

    public static float Distance(Vector3 left, Vector3 right) {
        return MathF.Sqrt(DistanceSquared(left, right));
    }

    public static Vector3 Lerp(Vector3 start, Vector3 end, float amount) {
        return new Vector3(
            float.Lerp(start.X, end.X, amount),
            float.Lerp(start.Y, end.Y, amount),
            float.Lerp(start.Z, end.Z, amount));
    }

    public Vector3 Normalize() {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            throw new InvalidOperationException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return this / MathF.Sqrt(lengthSquared);
    }

    public bool TryNormalize(out Vector3 result) {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            result = Zero;
            return false;
        }

        result = this / MathF.Sqrt(lengthSquared);
        return true;
    }

    public static Vector3 operator +(Vector3 left, Vector3 right) {
        return new Vector3(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    public static Vector3 operator -(Vector3 left, Vector3 right) {
        return new Vector3(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }

    public static Vector3 operator -(Vector3 value) {
        return new Vector3(-value.X, -value.Y, -value.Z);
    }

    public static Vector3 operator *(Vector3 value, float scale) {
        return new Vector3(value.X * scale, value.Y * scale, value.Z * scale);
    }

    public static Vector3 operator *(float scale, Vector3 value) {
        return value * scale;
    }

    public static Vector3 operator /(Vector3 value, float divisor) {
        return new Vector3(value.X / divisor, value.Y / divisor, value.Z / divisor);
    }
}
