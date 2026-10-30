namespace Melanopsin.Mathematics;

/// <summary>
/// An axis-aligned rectangle in two-dimensional space.
/// </summary>
public readonly record struct Rect {
    public Rect(float x, float y, float width, float height) {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rect(Point position, Size size)
        : this(position.X, position.Y, size.Width, size.Height) {
    }

    public float X { get; }

    public float Y { get; }

    public float Width { get; }

    public float Height { get; }

    public Point Position => new(X, Y);

    public Size Size => new(Width, Height);

    public Point Center => new(X + (Width / 2), Y + (Height / 2));

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(float x, float y) {
        return x >= X && x <= Right && y >= Y && y <= Bottom;
    }

    public bool Contains(Point point) {
        return Contains(point.X, point.Y);
    }

    public bool IntersectsWith(Rect other) {
        return !IsEmpty &&
               !other.IsEmpty &&
               other.X < Right &&
               other.Right > X &&
               other.Y < Bottom &&
               other.Bottom > Y;
    }

    public Rect Intersect(Rect other) {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new Rect(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top));
    }

    public Rect Union(Rect other) {
        if (IsEmpty) {
            return other;
        }

        if (other.IsEmpty) {
            return this;
        }

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    public Rect Inflate(float amount) {
        if (amount <= 0) {
            return this;
        }

        return new Rect(
            X - amount,
            Y - amount,
            Width + (amount * 2),
            Height + (amount * 2));
    }
}
