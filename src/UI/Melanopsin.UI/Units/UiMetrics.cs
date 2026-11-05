namespace Melanopsin.UI;

/// <summary>
/// Resolves density-independent UI units for one display context.
/// </summary>
public readonly record struct UiMetrics {
    public const float DefaultFontSize = 16;

    public UiMetrics(
        float scaleFactor,
        float rootFontSize = DefaultFontSize,
        float viewportWidth = 0,
        float viewportHeight = 0) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleFactor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootFontSize);
        ArgumentOutOfRangeException.ThrowIfNegative(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(viewportHeight);
        ScaleFactor = scaleFactor;
        RootFontSize = rootFontSize;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    public static UiMetrics Default => new(1);

    public float ScaleFactor { get; init; }

    public float RootFontSize { get; init; }

    public float ViewportWidth { get; init; }

    public float ViewportHeight { get; init; }

    public float Space(float step) => Step(step) * ScaleFactor;

    public float Radius(float step) => Step(step) * ScaleFactor;

    public float Font(float units) => units * ScaleFactor;

    public float Icon(float units) => units * ScaleFactor;

    public float Hairline => 1;

    public float DeviceIndependent(float value) => value * ScaleFactor;

    public float Point(float value) => value * ScaleFactor * (96f / 72f);

    internal bool HasSameCacheIdentity(UiMetrics other, bool compareViewport) {
        return ScaleFactor == other.ScaleFactor &&
            RootFontSize == other.RootFontSize &&
            (!compareViewport ||
                (ViewportWidth == other.ViewportWidth &&
                    ViewportHeight == other.ViewportHeight));
    }

    private static float Step(float value) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value * 4;
    }
}
