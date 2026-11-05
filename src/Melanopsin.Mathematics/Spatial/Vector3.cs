using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A direction and magnitude in three-dimensional space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Vector3 :
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

    /// <summary>
    /// Gets the squared magnitude. The sum of squares is formed in double
    /// precision, then rounded to <see cref="float"/>; a mathematical value
    /// above <see cref="float.MaxValue"/> therefore returns positive infinity.
    /// </summary>
    public float LengthSquared =>
        (float)(((double)X * X) + ((double)Y * Y) + ((double)Z * Z));

    /// <summary>
    /// Gets the magnitude without an avoidable intermediate squaring overflow.
    /// </summary>
    public float Length => (float)Math.Sqrt(
        ((double)X * X) + ((double)Y * Y) + ((double)Z * Z));

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z);

    public static float Dot(Vector3 left, Vector3 right) {
        return (float)(
            ((double)left.X * right.X) +
            ((double)left.Y * right.Y) +
            ((double)left.Z * right.Z));
    }

    public static Vector3 Cross(Vector3 left, Vector3 right) {
        return new Vector3(
            (float)(((double)left.Y * right.Z) - ((double)left.Z * right.Y)),
            (float)(((double)left.Z * right.X) - ((double)left.X * right.Z)),
            (float)(((double)left.X * right.Y) - ((double)left.Y * right.X)));
    }

    public static float DistanceSquared(Vector3 left, Vector3 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        var z = (double)left.Z - right.Z;
        return (float)((x * x) + (y * y) + (z * z));
    }

    public static float Distance(Vector3 left, Vector3 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        var z = (double)left.Z - right.Z;
        return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    public static Vector3 Lerp(Vector3 start, Vector3 end, float amount) {
        return new Vector3(
            float.Lerp(start.X, end.X, amount),
            float.Lerp(start.Y, end.Y, amount),
            float.Lerp(start.Z, end.Z, amount));
    }

    public Vector3 Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out Vector3 result) {
        return TryNormalize(out result, out _);
    }

    public static Vector3 Add(Vector3 left, Vector3 right) {
        return new Vector3(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    public static Vector3 Subtract(Vector3 left, Vector3 right) {
        return new Vector3(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }

    public static Vector3 Negate(Vector3 value) {
        return new Vector3(-value.X, -value.Y, -value.Z);
    }

    public static Vector3 Multiply(Vector3 value, float scale) {
        return new Vector3(value.X * scale, value.Y * scale, value.Z * scale);
    }

    public static Vector3 Multiply(float scale, Vector3 value) {
        return Multiply(value, scale);
    }

    public static Vector3 Divide(Vector3 value, float divisor) {
        return new Vector3(value.X / divisor, value.Y / divisor, value.Z / divisor);
    }

    public static Vector3 operator +(Vector3 left, Vector3 right) {
        return Add(left, right);
    }

    public static Vector3 operator -(Vector3 left, Vector3 right) {
        return Subtract(left, right);
    }

    public static Vector3 operator -(Vector3 value) {
        return Negate(value);
    }

    public static Vector3 operator *(Vector3 value, float scale) {
        return Multiply(value, scale);
    }

    public static Vector3 operator *(float scale, Vector3 value) {
        return Multiply(scale, value);
    }

    public static Vector3 operator /(Vector3 value, float divisor) {
        return Divide(value, divisor);
    }
}
