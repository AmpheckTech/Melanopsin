using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A direction and magnitude in two-dimensional space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Vector2 :
    IAdditionOperators<Vector2, Vector2, Vector2>,
    ISubtractionOperators<Vector2, Vector2, Vector2>,
    IUnaryNegationOperators<Vector2, Vector2>,
    IMultiplyOperators<Vector2, float, Vector2>,
    IDivisionOperators<Vector2, float, Vector2>,
    IAdditiveIdentity<Vector2, Vector2>,
    IEqualityOperators<Vector2, Vector2, bool> {
    public Vector2(float x, float y) {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => default;

    public static Vector2 One => new(1, 1);

    public static Vector2 UnitX => new(1, 0);

    public static Vector2 UnitY => new(0, 1);

    public static Vector2 AdditiveIdentity => Zero;

    public float X { get; }

    public float Y { get; }

    /// <summary>
    /// Gets the squared magnitude. The sum of squares is formed in double
    /// precision, then rounded to <see cref="float"/>; a mathematical value
    /// above <see cref="float.MaxValue"/> therefore returns positive infinity.
    /// </summary>
    public float LengthSquared =>
        (float)(((double)X * X) + ((double)Y * Y));

    /// <summary>
    /// Gets the magnitude without an avoidable intermediate squaring overflow.
    /// </summary>
    public float Length =>
        (float)Math.Sqrt(((double)X * X) + ((double)Y * Y));

    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);

    public static float Dot(Vector2 left, Vector2 right) {
        return (float)(
            ((double)left.X * right.X) +
            ((double)left.Y * right.Y));
    }

    /// <summary>
    /// Returns the signed two-dimensional cross product, which is the z
    /// component of the three-dimensional cross product.
    /// </summary>
    public static float Cross(Vector2 left, Vector2 right) {
        return (float)(
            ((double)left.X * right.Y) -
            ((double)left.Y * right.X));
    }

    public static float DistanceSquared(Vector2 left, Vector2 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        return (float)((x * x) + (y * y));
    }

    public static float Distance(Vector2 left, Vector2 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        return (float)Math.Sqrt((x * x) + (y * y));
    }

    public static Vector2 Lerp(Vector2 start, Vector2 end, float amount) {
        return new Vector2(
            float.Lerp(start.X, end.X, amount),
            float.Lerp(start.Y, end.Y, amount));
    }

    public Vector2 Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite vector cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out Vector2 result) {
        return TryNormalize(out result, out _);
    }

    public static Vector2 Add(Vector2 left, Vector2 right) {
        return new Vector2(left.X + right.X, left.Y + right.Y);
    }

    public static Vector2 Subtract(Vector2 left, Vector2 right) {
        return new Vector2(left.X - right.X, left.Y - right.Y);
    }

    public static Vector2 Negate(Vector2 value) {
        return new Vector2(-value.X, -value.Y);
    }

    public static Vector2 Multiply(Vector2 value, float scale) {
        return new Vector2(value.X * scale, value.Y * scale);
    }

    public static Vector2 Multiply(float scale, Vector2 value) {
        return Multiply(value, scale);
    }

    public static Vector2 Divide(Vector2 value, float divisor) {
        return new Vector2(value.X / divisor, value.Y / divisor);
    }

    public static Vector2 operator +(Vector2 left, Vector2 right) {
        return Add(left, right);
    }

    public static Vector2 operator -(Vector2 left, Vector2 right) {
        return Subtract(left, right);
    }

    public static Vector2 operator -(Vector2 value) {
        return Negate(value);
    }

    public static Vector2 operator *(Vector2 value, float scale) {
        return Multiply(value, scale);
    }

    public static Vector2 operator *(float scale, Vector2 value) {
        return Multiply(scale, value);
    }

    public static Vector2 operator /(Vector2 value, float divisor) {
        return Divide(value, divisor);
    }
}
