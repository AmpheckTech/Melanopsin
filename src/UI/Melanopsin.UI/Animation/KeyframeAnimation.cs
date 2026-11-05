// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Samples a set of keyframes over a shared timeline. Keyframes are sorted by
/// offset, and when multiple keyframes share an offset, the later input
/// keyframe wins at that point.
/// </summary>
public sealed class KeyframeAnimation<T> {
    private readonly Keyframe<T>[] _keyframes;
    private readonly Func<T, T, float, T> _interpolate;

    public KeyframeAnimation(
        IEnumerable<Keyframe<T>> keyframes,
        TimeSpan duration,
        Func<T, T, float, T> interpolate,
        Easing? easing = null,
        TimeSpan? delay = null,
        int iterations = 1,
        AnimationDirection direction = AnimationDirection.Forward) {
        ArgumentNullException.ThrowIfNull(keyframes);
        ArgumentNullException.ThrowIfNull(interpolate);
        _keyframes = keyframes.OrderBy(static keyframe => keyframe.Offset).ToArray();
        if (_keyframes.Length == 0) {
            throw new ArgumentException(
                "At least one keyframe is required.",
                nameof(keyframes));
        }

        _interpolate = interpolate;
        Timeline = new AnimationTimeline(
            duration,
            easing,
            delay,
            iterations,
            direction);
        Value = Sample(0);
    }

    public AnimationTimeline Timeline { get; }

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

    private T Sample(float progress) {
        var normalized = Math.Clamp(progress, 0, 1);
        if (normalized < _keyframes[0].Offset) {
            return _keyframes[0].Value;
        }

        var last = _keyframes[^1];
        if (normalized > last.Offset) {
            return last.Value;
        }

        var leftIndex = 0;
        while (leftIndex + 1 < _keyframes.Length &&
               _keyframes[leftIndex + 1].Offset <= normalized) {
            leftIndex++;
        }

        var left = _keyframes[leftIndex];
        if (leftIndex + 1 < _keyframes.Length) {
            var right = _keyframes[leftIndex + 1];
            var span = right.Offset - left.Offset;
            var amount = span <= 0
                ? 1
                : (normalized - left.Offset) / span;
            return _interpolate(left.Value, right.Value, amount);
        }

        return left.Value;
    }
}
