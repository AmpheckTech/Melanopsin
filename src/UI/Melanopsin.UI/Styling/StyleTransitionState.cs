namespace Melanopsin.UI.Styling;

internal sealed class StyleTransitionState {
    private AnimationTimeline _timeline;

    public StyleTransitionState(ResolvedStyle current, ResolvedStyle target) {
        Source = current;
        Current = current;
        Target = target;
        _timeline = CreateTimeline(target);
        _timeline.Play();
    }

    public ResolvedStyle Source { get; private set; }

    public ResolvedStyle Current { get; private set; }

    public ResolvedStyle Target { get; private set; }

    public float Progress => _timeline.Progress;

    public float EasedProgress => _timeline.EasedProgress;

    public bool IsComplete => _timeline.IsComplete;

    public bool Advance(float elapsedSeconds) {
        return _timeline.Advance(elapsedSeconds);
    }

    public void Retarget(ResolvedStyle target) {
        Source = Current;
        Target = target;
        _timeline = CreateTimeline(target);
        _timeline.Play();
    }

    public void SetCurrent(ResolvedStyle current) {
        Current = current;
    }

    private static AnimationTimeline CreateTimeline(ResolvedStyle target) {
        return new AnimationTimeline(
            TimeSpan.FromSeconds(Math.Max(0.001f, target.TransitionDuration)),
            target.TransitionEasing);
    }
}
