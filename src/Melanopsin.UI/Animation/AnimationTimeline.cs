namespace Melanopsin.UI.Animation;

/// <summary>
/// A repeatable, direction-aware time source. Timeline time is supplied by the
/// caller, so the type is independent of UI loops, schedulers, and wall clocks.
/// </summary>
public sealed class AnimationTimeline {
    private readonly TimeSpan _totalActiveDuration;
    private readonly TimeSpan _maximumElapsed;
    private bool _completionRaised;

    public AnimationTimeline(
        TimeSpan duration,
        Easing? easing = null,
        TimeSpan? delay = null,
        int iterations = 1,
        AnimationDirection direction = AnimationDirection.Forward) {
        if (duration <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Animation duration must be positive.");
        }

        if (iterations == 0 || iterations < -1) {
            throw new ArgumentOutOfRangeException(
                nameof(iterations),
                iterations,
                "Iterations must be positive or -1 for an infinite animation.");
        }

        Delay = delay ?? TimeSpan.Zero;
        if (Delay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                Delay,
                "Animation delay cannot be negative.");
        }

        Duration = duration;
        Easing = easing ?? Easing.Linear;
        Iterations = iterations;
        Direction = direction;
        var totalTicks = TimeSpan.MaxValue.Ticks;
        if (iterations > 0) {
            if (duration.Ticks > TimeSpan.MaxValue.Ticks / iterations) {
                throw new ArgumentOutOfRangeException(
                    nameof(iterations),
                    iterations,
                    "The total animation duration is too large.");
            }

            totalTicks = duration.Ticks * iterations;
            if (Delay.Ticks > TimeSpan.MaxValue.Ticks - totalTicks) {
                throw new ArgumentOutOfRangeException(
                    nameof(delay),
                    Delay,
                    "The animation delay plus total duration is too large.");
            }
        }

        _totalActiveDuration = new TimeSpan(totalTicks);
        _maximumElapsed = iterations > 0
            ? new TimeSpan(Delay.Ticks + totalTicks)
            : TimeSpan.MaxValue;
    }

    public TimeSpan Duration { get; }

    public TimeSpan Delay { get; }

    public Easing Easing { get; }

    public int Iterations { get; }

    public AnimationDirection Direction { get; }

    public AnimationState State { get; private set; } = AnimationState.Idle;

    public TimeSpan Elapsed { get; private set; }

    public TimeSpan ActiveElapsed => Elapsed <= Delay
        ? TimeSpan.Zero
        : Elapsed - Delay;

    public bool IsComplete => State == AnimationState.Completed;

    public bool IsPlaying => State == AnimationState.Running;

    public int CurrentIteration {
        get {
            if (ActiveElapsed <= TimeSpan.Zero) {
                return 0;
            }

            var iteration = ActiveElapsed.Ticks / Duration.Ticks;
            if (Iterations < 0) {
                return iteration > int.MaxValue ? int.MaxValue : (int)iteration;
            }

            return (int)Math.Min(iteration, Iterations - 1L);
        }
    }

    /// <summary>
    /// Linear progress in the current iteration after applying animation direction.
    /// The value is always within <c>[0, 1]</c>.
    /// </summary>
    public float Progress {
        get {
            if (Elapsed <= Delay) {
                return ApplyDirection(0, 0);
            }

            var active = ActiveElapsed;
            var progress = IsComplete
                ? 1f
                : (active.Ticks % Duration.Ticks) / (float)Duration.Ticks;
            return ApplyDirection(progress, CurrentIteration);
        }
    }

    /// <summary>
    /// Progress after applying the configured easing curve. Easing curves such
    /// as spring may intentionally overshoot the <c>[0, 1]</c> range.
    /// </summary>
    public float EasedProgress => Easing.Evaluate(Progress);

    /// <summary>
    /// Linear progress across all finite iterations. Infinite timelines remain
    /// at zero when active.
    /// </summary>
    public float TotalProgress {
        get {
            if (Iterations < 0 || ActiveElapsed <= TimeSpan.Zero) {
                return 0;
            }

            if (IsComplete) {
                return 1;
            }

            return (float)(ActiveElapsed.Ticks / (double)_totalActiveDuration.Ticks);
        }
    }

    /// <summary>
    /// Raised synchronously when <see cref="Advance(TimeSpan)"/> completes the
    /// timeline after the final active duration is reached.
    /// </summary>
    /// <remarks>
    /// Raised on the thread that calls <c>Advance</c>, before it returns, so
    /// handlers must not block. <see cref="Seek(float)"/> does not raise it.
    /// </remarks>
    public event EventHandler? Completed;

    /// <summary>
    /// Starts or resumes the timeline.
    /// </summary>
    /// <remarks>
    /// Playing a completed timeline resets it to zero and starts a new run;
    /// <see cref="AnimationState.Running"/> is a no-op.
    /// </remarks>
    public void Play() {
        if (State == AnimationState.Completed) {
            ResetTime();
        }

        State = AnimationState.Running;
    }

    public void Pause() {
        if (State == AnimationState.Running) {
            State = AnimationState.Paused;
        }
    }

    public void Restart() {
        ResetTime();
        State = AnimationState.Running;
    }

    public void Stop() {
        ResetTime();
    }

    /// <summary>
    /// Advances the timeline by a number of seconds.
    /// </summary>
    /// <remarks>
    /// Convenience overload that converts seconds at the 100-nanosecond tick
    /// resolution; use <see cref="Advance(TimeSpan)"/> for exact accumulated time.
    /// </remarks>
    public bool Advance(float deltaSeconds) {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Animation delta must be finite and non-negative.");
        }

        return Advance(TimeSpan.FromSeconds(deltaSeconds));
    }

    /// <summary>
    /// Advances a running timeline by <paramref name="delta"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the call changes elapsed time or completes the
    /// timeline; <see langword="false"/> while idle, paused, or already complete.
    /// </returns>
    public bool Advance(TimeSpan delta) {
        if (delta < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(delta),
                delta,
                "Animation delta cannot be negative.");
        }

        if (State != AnimationState.Running || delta == TimeSpan.Zero) {
            return false;
        }

        var previousState = State;
        if (Iterations < 0) {
            var nextElapsed = AddClamped(Elapsed, delta);
            var elapsedChanged = nextElapsed != Elapsed;
            Elapsed = nextElapsed;
            return elapsedChanged;
        }

        var next = Elapsed > _maximumElapsed - delta
            ? _maximumElapsed
            : Elapsed + delta;
        var changed = next != Elapsed;
        Elapsed = next;
        if (Elapsed >= _maximumElapsed) {
            State = AnimationState.Completed;
            if (!_completionRaised) {
                _completionRaised = true;
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        return changed || previousState != State;
    }

    /// <summary>
    /// Moves the timeline to a normalized position within its total active
    /// duration and pauses it.
    /// </summary>
    /// <remarks>
    /// Seeking to <c>1</c> marks the timeline complete without raising
    /// <see cref="Completed"/>.
    /// </remarks>
    public void Seek(float progress) {
        if (!float.IsFinite(progress)) {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress,
                "Seek progress must be finite.");
        }

        if (Iterations < 0) {
            throw new InvalidOperationException(
                "An infinite animation cannot be sought by total progress.");
        }

        var normalized = Math.Clamp(progress, 0, 1);
        Elapsed = Delay + TimeSpan.FromTicks(
            (long)Math.Round(_totalActiveDuration.Ticks * (double)normalized));
        _completionRaised = normalized >= 1;
        State = normalized >= 1
            ? AnimationState.Completed
            : AnimationState.Paused;
    }

    private float ApplyDirection(float progress, int iteration) {
        return Direction switch {
            AnimationDirection.Reverse => 1 - progress,
            AnimationDirection.Alternate => iteration % 2 == 0
                ? progress
                : 1 - progress,
            AnimationDirection.AlternateReverse => iteration % 2 == 0
                ? 1 - progress
                : progress,
            _ => progress,
        };
    }

    private void ResetTime() {
        Elapsed = TimeSpan.Zero;
        _completionRaised = false;
        State = AnimationState.Idle;
    }

    private static TimeSpan AddClamped(TimeSpan current, TimeSpan delta) {
        return current > TimeSpan.MaxValue - delta
            ? TimeSpan.MaxValue
            : current + delta;
    }
}
