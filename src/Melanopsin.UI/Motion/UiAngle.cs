using System.Globalization;

namespace Melanopsin.UI.Motion;

public readonly record struct UiAngle(float Degrees, AngleUnit Unit = AngleUnit.Degrees) {
    public static bool TryParse(string? text, out UiAngle angle) {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) {
            angle = default;
            return false;
        }

        var unit = AngleUnit.Degrees;
        if (value.EndsWith("deg", StringComparison.OrdinalIgnoreCase)) {
            value = value[..^3];
        } else if (value.EndsWith("rad", StringComparison.OrdinalIgnoreCase)) {
            unit = AngleUnit.Radians;
            value = value[..^3];
        } else if (value.EndsWith("turn", StringComparison.OrdinalIgnoreCase)) {
            unit = AngleUnit.Turns;
            value = value[..^4];
        }

        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)) {
            angle = default;
            return false;
        }

        var degrees = unit switch {
            AngleUnit.Radians => parsed * (180f / MathF.PI),
            AngleUnit.Turns => parsed * 360f,
            _ => parsed,
        };
        angle = new UiAngle(degrees, unit);
        return true;
    }

    public static UiAngle Parse(string text) {
        return TryParse(text, out var angle)
            ? angle
            : throw new FormatException($"Invalid angle '{text}'.");
    }
}
