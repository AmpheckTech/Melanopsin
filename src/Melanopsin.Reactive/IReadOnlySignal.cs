namespace Melanopsin.Reactive;

/// <summary>
/// A reactive value that can participate in dependency tracking.
/// </summary>
public interface IReadOnlySignal<out T> {
    /// <summary>
    /// Gets the current value and records a dependency on this signal.
    /// </summary>
    T Value { get; }

    /// <summary>
    /// Gets the current value without recording a dependency.
    /// </summary>
    T Peek();
}
