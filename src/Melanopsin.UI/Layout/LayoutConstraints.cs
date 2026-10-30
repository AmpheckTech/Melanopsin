namespace Melanopsin.UI.Layout;

/// <summary>
/// Defines the minimum and maximum size a layout participant may occupy.
/// Maximum values may be positive infinity to represent an unbounded axis.
/// </summary>
public readonly record struct LayoutConstraints {
    public LayoutConstraints(
        float minWidth,
        float maxWidth,
        float minHeight,
        float maxHeight) {
        ValidateAxis(minWidth, maxWidth, nameof(minWidth), nameof(maxWidth));
        ValidateAxis(minHeight, maxHeight, nameof(minHeight), nameof(maxHeight));
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    public float MinWidth { get; }

    public float MaxWidth { get; }

    public float MinHeight { get; }

    public float MaxHeight { get; }

    public bool HasBoundedWidth => float.IsFinite(MaxWidth);

    public bool HasBoundedHeight => float.IsFinite(MaxHeight);

    public bool IsWidthTight => MinWidth.Equals(MaxWidth);

    public bool IsHeightTight => MinHeight.Equals(MaxHeight);

    public static LayoutConstraints Unbounded { get; } = new(
        0,
        float.PositiveInfinity,
        0,
        float.PositiveInfinity);

    public static LayoutConstraints Loose(float maxWidth, float maxHeight) {
        return new LayoutConstraints(0, maxWidth, 0, maxHeight);
    }

    public static LayoutConstraints Tight(LayoutSize size) {
        return Tight(size.Width, size.Height);
    }

    public static LayoutConstraints Tight(float width, float height) {
        return new LayoutConstraints(width, width, height, height);
    }

    public static LayoutConstraints FromMeasureConstraint(
        MeasureConstraint constraint) {
        return Loose(constraint.MaxWidth, constraint.MaxHeight);
    }

    public LayoutSize Constrain(LayoutSize size) {
        return new LayoutSize(
            ConstrainWidth(size.Width),
            ConstrainHeight(size.Height));
    }

    public float ConstrainWidth(float width) {
        return Math.Clamp(width, MinWidth, MaxWidth);
    }

    public float ConstrainHeight(float height) {
        return Math.Clamp(height, MinHeight, MaxHeight);
    }

    public LayoutConstraints Deflate(float horizontal, float vertical) {
        ArgumentOutOfRangeException.ThrowIfNegative(horizontal);
        ArgumentOutOfRangeException.ThrowIfNegative(vertical);
        return new LayoutConstraints(
            Math.Max(0, MinWidth - horizontal),
            Math.Max(0, MaxWidth - horizontal),
            Math.Max(0, MinHeight - vertical),
            Math.Max(0, MaxHeight - vertical));
    }

    private static void ValidateAxis(
        float minimum,
        float maximum,
        string minimumName,
        string maximumName) {
        if (float.IsNaN(minimum) ||
            float.IsNegativeInfinity(minimum) ||
            minimum < 0) {
            throw new ArgumentOutOfRangeException(
                minimumName,
                minimum,
                "A minimum size must be finite and non-negative.");
        }

        if (float.IsNaN(maximum) ||
            float.IsNegativeInfinity(maximum) ||
            maximum < minimum) {
            throw new ArgumentOutOfRangeException(
                maximumName,
                maximum,
                "A maximum size must be non-negative and at least the minimum.");
        }
    }
}
