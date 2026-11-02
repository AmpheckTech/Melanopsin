namespace Melanopsin.UI.Styling;

/// <summary>
/// A typed CSS blur filter. <see cref="None"/> resets the effect when it is
/// declared by a later cascade rule.
/// </summary>
public readonly record struct BlurFilter {
    private BlurFilter(Length radius) {
        Radius = radius;
    }

    public Length Radius { get; }

    public static BlurFilter None => new(Length.Zero);

    public static BlurFilter Blur(Length radius) {
        if (!float.IsFinite(radius.Value) ||
            radius.Value < 0 ||
            radius.Unit is LengthUnit.Auto or
            LengthUnit.Percent or
            LengthUnit.Fraction) {
            throw new ArgumentException(
                "A blur radius requires a resolvable length, not auto, a percentage, or a fraction.",
                nameof(radius));
        }

        return new BlurFilter(radius);
    }
}
