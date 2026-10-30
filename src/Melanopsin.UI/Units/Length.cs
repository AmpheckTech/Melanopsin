using System.Globalization;

namespace Melanopsin.UI;

public readonly record struct Length(float Value, LengthUnit Unit) {
    public static Length Auto { get; } = new(0, LengthUnit.Auto);

    public static Length Pixels(float value) {
        if (!float.IsFinite(value) || value < 0) {
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        return new Length(value, LengthUnit.Pixels);
    }

    public static Length Percent(float value) {
        if (!float.IsFinite(value) || value < 0) {
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        return new Length(value, LengthUnit.Percent);
    }

    public static Length Space(float steps) => UnitValue(steps, LengthUnit.SpaceUnit);

    public static Length Radius(float steps) => UnitValue(steps, LengthUnit.RadiusUnit);

    public static Length Font(float units) => UnitValue(units, LengthUnit.FontUnit);

    public static Length Icon(float units) => UnitValue(units, LengthUnit.IconUnit);

    public static Length Hairline(float count = 1) => UnitValue(count, LengthUnit.Hairline);

    public static Length ViewportWidth(float percent) =>
        UnitValue(percent, LengthUnit.ViewportWidth);

    public static Length ViewportHeight(float percent) =>
        UnitValue(percent, LengthUnit.ViewportHeight);

    public static Length Fraction(float value) => UnitValue(value, LengthUnit.Fraction);

    public static Length Rem(float value) => UnitValue(value, LengthUnit.Rem);

    public static Length Em(float value) => UnitValue(value, LengthUnit.Em);

    public static Length DeviceIndependent(float value) =>
        UnitValue(value, LengthUnit.DeviceIndependentPixel);

    public static Length ScaleIndependent(float value) =>
        UnitValue(value, LengthUnit.ScaleIndependentPixel);

    public static Length Point(float value) => UnitValue(value, LengthUnit.Point);

    /// <summary>
    /// Whether resolving this length depends on the current viewport size.
    /// </summary>
    public bool UsesViewportUnits =>
        Unit is LengthUnit.ViewportWidth or LengthUnit.ViewportHeight;

    public float Resolve(float available, float fallback) {
        return Resolve(UiMetrics.Default, available, fallback);
    }

    public float Resolve(
        UiMetrics metrics,
        float available,
        float fallback,
        float emSize = 0) {
        return Unit switch {
            LengthUnit.Pixels => Value,
            LengthUnit.Percent => available * (Value / 100f),
            LengthUnit.SpaceUnit => metrics.Space(Value),
            LengthUnit.RadiusUnit => metrics.Radius(Value),
            LengthUnit.FontUnit => metrics.Font(Value),
            LengthUnit.IconUnit => metrics.Icon(Value),
            LengthUnit.Hairline => metrics.Hairline * Value,
            LengthUnit.ViewportWidth => metrics.ViewportWidth * (Value / 100f),
            LengthUnit.ViewportHeight => metrics.ViewportHeight * (Value / 100f),
            LengthUnit.Fraction => available * Value,
            LengthUnit.Rem => metrics.Font(metrics.RootFontSize * Value),
            LengthUnit.Em => (emSize > 0 ? emSize : metrics.Font(metrics.RootFontSize)) * Value,
            LengthUnit.DeviceIndependentPixel => metrics.DeviceIndependent(Value),
            LengthUnit.ScaleIndependentPixel => metrics.DeviceIndependent(Value),
            LengthUnit.Point => metrics.Point(Value),
            _ => fallback,
        };
    }

    public static bool TryParse(string? text, out Length length) {
        if (string.Equals(text?.Trim(), "auto", StringComparison.OrdinalIgnoreCase)) {
            length = Auto;
            return true;
        }

        if (string.IsNullOrWhiteSpace(text)) {
            length = default;
            return false;
        }

        var value = text.Trim();
        var unit = LengthUnit.Pixels;
        if (!TryReadDimensionUnit(ref value, ref unit) && value.EndsWith('%')) {
            unit = LengthUnit.Percent;
            value = value[..^1];
        } else if (unit == LengthUnit.Pixels &&
                   value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) {
            value = value[..^2];
        }

        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            !float.IsFinite(parsed) ||
            parsed < 0) {
            length = default;
            return false;
        }

        length = new Length(parsed, unit);
        return true;
    }

    private static bool TryReadDimensionUnit(ref string value, ref LengthUnit unit) {
        return TryReadSuffix(ref value, "su", LengthUnit.SpaceUnit, ref unit) ||
            TryReadSuffix(ref value, "ru", LengthUnit.RadiusUnit, ref unit) ||
            TryReadSuffix(ref value, "fu", LengthUnit.FontUnit, ref unit) ||
            TryReadSuffix(ref value, "iu", LengthUnit.IconUnit, ref unit) ||
            TryReadSuffix(ref value, "hx", LengthUnit.Hairline, ref unit) ||
            TryReadSuffix(ref value, "vw", LengthUnit.ViewportWidth, ref unit) ||
            TryReadSuffix(ref value, "vh", LengthUnit.ViewportHeight, ref unit) ||
            TryReadSuffix(ref value, "fr", LengthUnit.Fraction, ref unit) ||
            TryReadSuffix(ref value, "rem", LengthUnit.Rem, ref unit) ||
            TryReadSuffix(ref value, "em", LengthUnit.Em, ref unit) ||
            TryReadSuffix(ref value, "dp", LengthUnit.DeviceIndependentPixel, ref unit) ||
            TryReadSuffix(ref value, "sp", LengthUnit.ScaleIndependentPixel, ref unit) ||
            TryReadSuffix(ref value, "pt", LengthUnit.Point, ref unit);
    }

    private static bool TryReadSuffix(
        ref string value,
        string suffix,
        LengthUnit candidate,
        ref LengthUnit unit) {
        if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        value = value[..^suffix.Length];
        unit = candidate;
        return true;
    }

    private static Length UnitValue(float value, LengthUnit unit) {
        if (!float.IsFinite(value) || value <= 0) {
            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        return new Length(value, unit);
    }

    public static Length Parse(string text) {
        return TryParse(text, out var length)
            ? length
            : throw new FormatException($"Invalid length '{text}'.");
    }
}
