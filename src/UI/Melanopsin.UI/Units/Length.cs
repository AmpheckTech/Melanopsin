// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

public readonly record struct Length {
    public Length(float value, LengthUnit unit) {
        if (!float.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A length must be finite.");
        }

        if (!Enum.IsDefined(unit)) {
            throw new ArgumentOutOfRangeException(
                nameof(unit),
                unit,
                "The length unit is not defined.");
        }

        Value = value;
        Unit = unit;
    }

    public float Value { get; }

    public LengthUnit Unit { get; }

    public void Deconstruct(out float value, out LengthUnit unit) {
        value = Value;
        unit = Unit;
    }

    public static Length Auto { get; } = new(0, LengthUnit.Auto);

    public static Length Zero { get; } = new(0, LengthUnit.Pixels);

    public static Length Px(float value) => UnitValue(value, LengthUnit.Pixels);

    public static Length Percent(float value) => UnitValue(value, LengthUnit.Percent);

    public static Length Su(float steps) => UnitValue(steps, LengthUnit.SpaceUnit);

    public static Length Ru(float steps) => UnitValue(steps, LengthUnit.RadiusUnit);

    public static Length Fu(float units) => UnitValue(units, LengthUnit.FontUnit);

    public static Length Iu(float units) => UnitValue(units, LengthUnit.IconUnit);

    public static Length Hx(float count = 1) => UnitValue(count, LengthUnit.Hairline);

    public static Length Vw(float percent) =>
        UnitValue(percent, LengthUnit.ViewportWidth);

    public static Length Vh(float percent) =>
        UnitValue(percent, LengthUnit.ViewportHeight);

    public static Length Fr(float value) => UnitValue(value, LengthUnit.Fraction);

    public static Length Rem(float value) => UnitValue(value, LengthUnit.Rem);

    public static Length Em(float value) => UnitValue(value, LengthUnit.Em);

    public static Length Dp(float value) =>
        UnitValue(value, LengthUnit.DeviceIndependentPixel);

    public static Length Sp(float value) =>
        UnitValue(value, LengthUnit.ScaleIndependentPixel);

    public static Length Pt(float value) => UnitValue(value, LengthUnit.Point);

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

    private static Length UnitValue(float value, LengthUnit unit) => new(value, unit);
}
