namespace Melanopsin.UI.Animation;

/// <summary>
/// Accelerates through the first half and decelerates through the second.
/// </summary>
public sealed record EaseInOutEasing : Easing {
    protected override float EvaluateNormalized(float progress) {
        if (progress < 0.5f) {
            return 4 * progress * progress * progress;
        }

        var inverse = (-2 * progress) + 2;
        return 1 - ((inverse * inverse * inverse) / 2);
    }
}
