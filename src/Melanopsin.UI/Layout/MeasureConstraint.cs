namespace Melanopsin.UI.Layout;

/// <summary>
/// Compatibility constraint used by the original layout engine API.
/// New layout code should use <see cref="LayoutConstraints"/>.
/// </summary>
public readonly record struct MeasureConstraint(float MaxWidth, float MaxHeight) {
    public static MeasureConstraint FromSize(PixelSize size) {
        return new MeasureConstraint(size.Width, size.Height);
    }

    public LayoutConstraints ToLayoutConstraints() {
        return LayoutConstraints.FromMeasureConstraint(this);
    }
}
