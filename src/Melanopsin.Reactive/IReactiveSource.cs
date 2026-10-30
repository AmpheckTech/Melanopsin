namespace Melanopsin.Reactive;

/// <summary>
/// A value source that can notify reactive consumers when it changes.
/// </summary>
public interface IReactiveSource {
    event Action<IReactiveSource>? Invalidated;
}
