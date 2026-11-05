namespace Melanopsin.UI.Animation;

/// <summary>
/// A cubic-bezier easing curve with X control points constrained to [0, 1].
/// </summary>
public sealed record CubicBezierEasing : Easing {
    private const float ParameterTolerance = 1e-7f;
    private const int MaxSolverIterations = 24;

    public CubicBezierEasing(float x1, float y1, float x2, float y2) {
        if (!float.IsFinite(x1) || x1 is < 0 or > 1 ||
            !float.IsFinite(y1) ||
            !float.IsFinite(x2) || x2 is < 0 or > 1 ||
            !float.IsFinite(y2)) {
            throw new ArgumentOutOfRangeException(
                nameof(x1),
                "Cubic bezier control points must be finite and X controls must be within [0, 1].");
        }

        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public float X1 { get; }

    public float Y1 { get; }

    public float X2 { get; }

    public float Y2 { get; }

    protected override float EvaluateNormalized(float progress) {
        var parameter = progress;
        var lower = 0f;
        var upper = 1f;
        for (var iteration = 0; iteration < MaxSolverIterations; iteration++) {
            var x = Bezier(parameter, X1, X2) - progress;
            if (x == 0) {
                return Bezier(parameter, Y1, Y2);
            }

            if (x < 0) {
                lower = parameter;
            } else {
                upper = parameter;
            }

            if (upper - lower <= ParameterTolerance) {
                break;
            }

            var derivative = BezierDerivative(parameter, X1, X2);
            var candidate = derivative > 0
                ? parameter - (x / derivative)
                : float.NaN;
            parameter = !float.IsFinite(candidate) ||
                candidate <= lower ||
                candidate >= upper
                ? (lower + upper) / 2
                : candidate;
        }

        return Bezier((lower + upper) / 2, Y1, Y2);
    }

    private static float Bezier(
        float parameter,
        float control1,
        float control2) {
        var inverse = 1 - parameter;
        return (3 * inverse * inverse * parameter * control1) +
            (3 * inverse * parameter * parameter * control2) +
            (parameter * parameter * parameter);
    }

    private static float BezierDerivative(
        float parameter,
        float control1,
        float control2) {
        var inverse = 1 - parameter;
        return (3 * inverse * inverse * control1) +
            (6 * inverse * parameter * (control2 - control1)) +
            (3 * parameter * parameter * (1 - control2));
    }
}
