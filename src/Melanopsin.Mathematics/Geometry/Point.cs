namespace Melanopsin.Mathematics;

/// <summary>
/// A position in two-dimensional space.
/// </summary>
public readonly record struct Point {
    public Point(float x, float y) {
        X = x;
        Y = y;
    }

    public static Point Origin => default;

    public float X { get; }

    public float Y { get; }

    public static Point operator +(Point point, Vector vector) {
        return new Point(point.X + vector.X, point.Y + vector.Y);
    }

    public static Point operator -(Point point, Vector vector) {
        return new Point(point.X - vector.X, point.Y - vector.Y);
    }

    public static Vector operator -(Point left, Point right) {
        return new Vector(left.X - right.X, left.Y - right.Y);
    }
}
