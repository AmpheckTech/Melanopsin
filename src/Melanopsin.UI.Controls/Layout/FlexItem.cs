namespace Melanopsin.UI.Controls;

public readonly record struct FlexItem {
    private readonly bool _isInitialized;

    public FlexItem()
        : this(
            Grow: 0,
            Shrink: 1,
            Order: 0,
            AlignSelf: LayoutAlignment.Auto) {
    }

    public FlexItem(
        float Grow = 0,
        float Shrink = 1,
        int Order = 0,
        LayoutAlignment AlignSelf = LayoutAlignment.Auto) {
        this.Grow = ValidateScale(Grow, nameof(Grow));
        this.Shrink = ValidateScale(Shrink, nameof(Shrink));
        this.Order = Order;
        this.AlignSelf = AlignSelf;
        _isInitialized = true;
    }

    public float Grow { get; }

    public float Shrink { get; }

    public int Order { get; }

    public LayoutAlignment AlignSelf { get; }

    internal bool IsInitialized => _isInitialized;

    private static float ValidateScale(float value, string name) {
        return float.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                name,
                value,
                "Flex item scale values must be finite and non-negative.");
    }
}
