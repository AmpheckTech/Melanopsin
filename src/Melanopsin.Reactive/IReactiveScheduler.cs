namespace Melanopsin.Reactive;

/// <summary>
/// Dispatches reactive notifications when a source is changed from another
/// thread.
/// </summary>
public interface IReactiveScheduler {
    bool IsStopped { get; }

    bool CheckAccess();

    bool TryPost(Action action);
}
