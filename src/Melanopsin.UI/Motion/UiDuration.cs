namespace Melanopsin.UI.Motion;

public readonly record struct UiDuration(float Milliseconds, DurationUnit Unit = DurationUnit.Milliseconds) {
    public static UiDuration Ms(float milliseconds) {
        if (!float.IsFinite(milliseconds) || milliseconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, null);
        }

        return new UiDuration(milliseconds, DurationUnit.Milliseconds);
    }

    public static UiDuration S(float seconds) {
        if (!float.IsFinite(seconds) || seconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, null);
        }

        return new UiDuration(seconds * 1000, DurationUnit.Seconds);
    }

    public static UiDuration Steps(int steps) {
        return Step(steps);
    }

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
}
