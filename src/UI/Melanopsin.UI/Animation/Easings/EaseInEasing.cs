namespace Melanopsin.UI.Animation;

/// <summary>
/// Starts slowly and accelerates toward the endpoint.
/// </summary>
public sealed record EaseInEasing : Easing {
    protected override float EvaluateNormalized(float progress) {
        return progress * progress * progress;
    }
}
