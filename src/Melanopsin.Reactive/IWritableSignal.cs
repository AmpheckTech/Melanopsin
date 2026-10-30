namespace Melanopsin.Reactive;

/// <summary>
/// A reactive value whose current value can be changed.
/// </summary>
public interface IWritableSignal<T> : IReadOnlySignal<T> {
    new T Value { get; set; }

    /// <summary>
    /// Notifies consumers even if the current value has not changed.
    /// </summary>
    void Invalidate();
}
