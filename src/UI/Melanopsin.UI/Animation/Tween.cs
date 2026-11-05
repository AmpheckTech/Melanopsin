// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Animates a value between two endpoints using a caller-provided interpolator.
/// </summary>
public sealed class Tween<T> {
    private readonly Func<T, T, float, T> _interpolate;

    public Tween(
        T from,
        T to,
        TimeSpan duration,
        Func<T, T, float, T> interpolate,
        Easing? easing = null,
        TimeSpan? delay = null,
        int iterations = 1,
        AnimationDirection direction = AnimationDirection.Forward) {
        ArgumentNullException.ThrowIfNull(interpolate);
        From = from;
        To = to;
        _interpolate = interpolate;
        Timeline = new AnimationTimeline(
            duration,
            easing,
            delay,
            iterations,
            direction);
        Value = from;
    }

    public AnimationTimeline Timeline { get; }

    public T From { get; private set; }

    public T To { get; private set; }

    public T Value { get; private set; }

    public AnimationState State => Timeline.State;

    public bool IsComplete => Timeline.IsComplete;

    public bool IsPlaying => Timeline.IsPlaying;

    public bool Advance(float deltaSeconds) {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Animation delta must be finite and non-negative.");
        }

        return Advance(TimeSpan.FromSeconds(deltaSeconds));
    }

    public bool Advance(TimeSpan delta) {
        if (!Timeline.Advance(delta)) {
            return false;
        }

        Value = Sample(Timeline.EasedProgress);
        return true;
    }

    public void Play() {
        Timeline.Play();
        Value = Sample(Timeline.EasedProgress);
    }

    public void Pause() {
        Timeline.Pause();
    }

    public void Restart() {
        Timeline.Restart();
        Value = Sample(Timeline.EasedProgress);
    }

    public void Stop() {
        Timeline.Stop();
        Value = Sample(Timeline.EasedProgress);
    }

    /// <summary>
    /// Moves directly to a normalized position without playing the timeline.
    /// </summary>
    public void Seek(float progress) {
        Timeline.Seek(progress);
        Value = Sample(Timeline.EasedProgress);
    }

    public void Retarget(T to) {
        From = Value;
        To = to;
        Timeline.Restart();
        Value = Sample(Timeline.EasedProgress);
    }

    private T Sample(float progress) {
        return _interpolate(From, To, progress);
    }
}
