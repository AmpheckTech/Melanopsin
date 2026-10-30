using System.Globalization;

namespace Melanopsin.UI.Motion;

public readonly record struct UiDuration(float Milliseconds, DurationUnit Unit = DurationUnit.Milliseconds) {
    public static UiDuration Step(int step) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var milliseconds = step switch {
            1 => 100,
            2 => 200,
            3 => 300,
            4 => 400,
            5 => 500,
            6 => 600,
            _ => step * 100,
        };
        return new UiDuration(milliseconds, DurationUnit.Step);
    }

    public static bool TryParse(string? text, out UiDuration duration) {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) {
            duration = default;
            return false;
        }

        if (value.EndsWith("du", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                value[..^2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var step) &&
            step > 0) {
            duration = Step(step);
            return true;
        }

        var unit = DurationUnit.Milliseconds;
        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)) {
            value = value[..^2];
        } else if (value.EndsWith('s')) {
            unit = DurationUnit.Seconds;
            value = value[..^1];
        }

        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed < 0) {
            duration = default;
            return false;
        }

        duration = unit == DurationUnit.Seconds
            ? new UiDuration(parsed * 1000, unit)
            : new UiDuration(parsed, unit);
        return true;
    }

    public static UiDuration Parse(string text) {
        return TryParse(text, out var duration)
            ? duration
            : throw new FormatException($"Invalid duration '{text}'.");
    }
}
