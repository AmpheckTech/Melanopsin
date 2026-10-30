namespace Melanopsin.Mathematics;

/// <summary>
/// A direction and magnitude in two-dimensional space.
/// </summary>
public readonly record struct Vector {
    public Vector(float x, float y) {
        X = x;
        Y = y;
    }

    public static Vector Zero => default;

    public static Vector UnitX => new(1, 0);

    public static Vector UnitY => new(0, 1);

    public float X { get; }

    public float Y { get; }

    public float LengthSquared => (X * X) + (Y * Y);

    public float Length => MathF.Sqrt(LengthSquared);

    public static float Dot(Vector left, Vector right) {
        return (left.X * right.X) + (left.Y * right.Y);
    }

    public static Vector operator +(Vector left, Vector right) {
        return new Vector(left.X + right.X, left.Y + right.Y);
    }

    public static Vector operator -(Vector left, Vector right) {
        return new Vector(left.X - right.X, left.Y - right.Y);
    }

    public static Vector operator -(Vector value) {
        return new Vector(-value.X, -value.Y);
    }

    public static Vector operator *(Vector value, float scale) {
        return new Vector(value.X * scale, value.Y * scale);
    }

    public static Vector operator *(float scale, Vector value) {
        return value * scale;
    }

    public static Vector operator /(Vector value, float divisor) {
        return new Vector(value.X / divisor, value.Y / divisor);
    }
}
