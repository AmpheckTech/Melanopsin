namespace Melanopsin.UI.Animation;

/// <summary>
/// A value at a normalized offset within an animation.
/// </summary>
/// <typeparam name="T">The animated value type.</typeparam>
public readonly record struct Keyframe<T> {
    /// <summary>
    /// Creates a keyframe.
    /// </summary>
    /// <param name="offset">Normalized position in the range <c>[0, 1]</c>.</param>
    /// <param name="value">Value sampled at <paramref name="offset"/>.</param>
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

    /// <summary>
    /// Gets the value sampled at <see cref="Offset"/>.
    /// </summary>
    public T Value { get; }
}
