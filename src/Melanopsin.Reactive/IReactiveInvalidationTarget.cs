namespace Melanopsin.Reactive;

/// <summary>
/// A consumer whose work must be repeated after a dependency changes.
/// </summary>
public interface IReactiveInvalidationTarget {
    void InvalidateFromReactiveSource();
}
