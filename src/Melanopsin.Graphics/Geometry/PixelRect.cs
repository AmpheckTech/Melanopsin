namespace Melanopsin.Graphics;

public readonly record struct PixelRect {
    public PixelRect(int x, int y, int width, int height) {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width == 0 || Height == 0;

    public long Area => (long)Width * Height;

    public static PixelRect FromSize(PixelSize size) {
        return new PixelRect(0, 0, size.Width, size.Height);
    }

    public bool IntersectsWith(PixelRect other) {
        return !IsEmpty &&
               !other.IsEmpty &&
               other.X < Right &&
               other.Right > X &&
               other.Y < Bottom &&
               other.Bottom > Y;
    }

    public bool Touches(PixelRect other) {
        return other.X <= Right &&
               other.Right >= X &&
               other.Y <= Bottom &&
               other.Bottom >= Y;
    }

    public PixelRect Union(PixelRect other) {
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
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
