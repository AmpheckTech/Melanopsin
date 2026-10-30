namespace Melanopsin.UI.Animation;

/// <summary>
/// Describes how normalized time is mapped to animation progress.
/// </summary>
public abstract record Easing {
    public static Easing Linear { get; } = new LinearEasing();

    public static Easing EaseOut { get; } = new EaseOutEasing();

    public static Easing EaseIn { get; } = new EaseInEasing();

    public static Easing EaseInOut { get; } = new EaseInOutEasing();

    public static Easing Spring { get; } = new SpringEasing();

    private protected Easing() {
    }

    public static Easing CubicBezier(float x1, float y1, float x2, float y2) {
        return new CubicBezierEasing(x1, y1, x2, y2);
    }

    public static Easing Step(
        int steps,
        StepPosition position = StepPosition.JumpEnd) {
        return new StepEasing(steps, position);
    }

    /// <summary>
    /// Evaluates the easing curve for normalized progress.
    /// </summary>
    /// <remarks>
    /// Cubic-bezier curves resolve their time parameter to approximately
    /// <c>1e-7</c>. Their output error can scale with the magnitude of the Y
    /// control points, so this is a parameter-space tolerance rather than an
    /// absolute output tolerance.
    /// </remarks>
    public float Evaluate(float progress) {
        if (!float.IsFinite(progress)) {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress,
                "Easing progress must be finite.");
        }

        var normalized = Math.Clamp(progress, 0, 1);
        if (normalized <= 0) {
            return 0;
        }

        if (normalized >= 1) {
            return 1;
        }

        return EvaluateNormalized(normalized);
    }

    protected abstract float EvaluateNormalized(float progress);
}
