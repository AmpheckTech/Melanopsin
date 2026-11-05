// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// A value at a normalized offset within an animation.
/// </summary>
/// <typeparam name="T">The animated value type.</typeparam>
public readonly record struct Keyframe<T> {
    public Keyframe(float offset, T value) {
        if (!float.IsFinite(offset) || offset is < 0 or > 1) {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Keyframe offset must be within [0, 1].");
        }

        Offset = offset;
        Value = value;
    }

    public float Offset { get; }

    public T Value { get; }
}
