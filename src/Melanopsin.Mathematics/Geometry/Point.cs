using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A position in two-dimensional space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Point {
    public Point(float x, float y) {
        X = x;
        Y = y;
    }

    public static Point Origin => default;

    public float X { get; }

    public float Y { get; }

    public static Point Add(Point point, Vector2 vector) {
        return new Point(point.X + vector.X, point.Y + vector.Y);
    }

    public static Point Subtract(Point point, Vector2 vector) {
        return new Point(point.X - vector.X, point.Y - vector.Y);
    }

    public static Vector2 Subtract(Point left, Point right) {
        return new Vector2(left.X - right.X, left.Y - right.Y);
    }

    public static Point operator +(Point point, Vector2 vector) {
        return Add(point, vector);
    }

    public static Point operator -(Point point, Vector2 vector) {
        return Subtract(point, vector);
    }

    public static Vector2 operator -(Point left, Point right) {
        return Subtract(left, right);
    }
}
