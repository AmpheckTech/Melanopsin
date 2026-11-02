using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// Scalar helpers shared by the optional three-dimensional math types.
/// </summary>
public static class ScalarMath {
    public const float Epsilon = 0.000001f;

    internal const float EpsilonSquared = Epsilon * Epsilon;

    public static float DegreesToRadians(float degrees) {
        return degrees * (MathF.PI / 180);
    }

    public static float RadiansToDegrees(float radians) {
        return radians * (180 / MathF.PI);
    }

    public static float Lerp(float start, float end, float amount) {
        return float.Lerp(start, end, amount);
    }

    public static T Lerp<T>(T start, T end, T amount)
        where T : INumber<T> {
        return start + ((end - start) * amount);
    }

    public static float Clamp(float value, float minimum, float maximum) {
        return Math.Clamp(value, minimum, maximum);
    }

    public static T Clamp<T>(T value, T minimum, T maximum)
        where T : INumber<T> {
        if (value < minimum) {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    public static T Min<T>(T left, T right)
        where T : INumber<T> {
        return left < right ? left : right;
    }

    public static T Max<T>(T left, T right)
        where T : INumber<T> {
        return left > right ? left : right;
    }

    public static float Clamp01(float value) {
        return Math.Clamp(value, 0, 1);
    }

    public static T Clamp01<T>(T value)
        where T : INumber<T> {
        return Clamp(value, T.Zero, T.One);
    }

    public static bool ApproximatelyEquals<T>(
        T left,
        T right,
        T tolerance)
        where T : IFloatingPointIeee754<T> {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        return T.Abs(left - right) <= tolerance;
    }

    public static bool ApproximatelyEquals(
        float left,
        float right,
        float tolerance = Epsilon) {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        return MathF.Abs(left - right) <= tolerance;
    }

    public static bool ApproximatelyEquals(
        Vector3 left,
        Vector3 right,
        float tolerance = Epsilon) {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        return ApproximatelyEquals(left.X, right.X, tolerance) &&
            ApproximatelyEquals(left.Y, right.Y, tolerance) &&
            ApproximatelyEquals(left.Z, right.Z, tolerance);
    }

    public static bool ApproximatelyEquals(
        Vector4 left,
        Vector4 right,
        float tolerance = Epsilon) {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        return ApproximatelyEquals(left.X, right.X, tolerance) &&
            ApproximatelyEquals(left.Y, right.Y, tolerance) &&
            ApproximatelyEquals(left.Z, right.Z, tolerance) &&
            ApproximatelyEquals(left.W, right.W, tolerance);
    }
}
