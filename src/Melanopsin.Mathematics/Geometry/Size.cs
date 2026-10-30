namespace Melanopsin.Mathematics;

/// <summary>
/// A non-negative two-dimensional extent.
/// </summary>
public readonly record struct Size {
    public Size(float width, float height) {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
    }

    public static Size Empty => default;

    public float Width { get; }

    public float Height { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
