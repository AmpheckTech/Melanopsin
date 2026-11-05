using System.Runtime.ExceptionServices;

namespace Melanopsin.UI.Threading;

public sealed partial class UiDispatcher {
    /// <summary>
    /// Requests shutdown and waits until the owner thread has drained and
    /// finalized. Prefer <see cref="StopAsync(CancellationToken)"/> from asynchronous code.
    /// </summary>
    public void Stop() {
        var stopTask = StopAsync();
        if (CheckAccess() && !stopTask.IsCompleted) {
            return;
        }

        stopTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Requests shutdown. Owner-thread calls finalize synchronously; other threads
    /// wait asynchronously. Cancellation abandons the wait but not the shutdown.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default) {
        return StopAsyncCore(shutdownFinalizer: null, cancellationToken);
    }

    internal Task StopAsync(
        Action<Exception?> shutdownFinalizer,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(shutdownFinalizer);
        return StopAsyncCore(shutdownFinalizer, cancellationToken);
    }

    private Task StopAsyncCore(
        Action<Exception?>? shutdownFinalizer,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var request = RequestStop(shutdownFinalizer);
        if ((request.WasCreated || CheckAccess()) &&
            TryBeginShutdownCompletion()) {
            var failure = CompleteStop();
            return failure is null
                ? Task.CompletedTask
                : Task.FromException(failure);
        }

        if (request.DeferredFinalizer is not null) {
            ScheduleDeferredFinalizer(request.DeferredFinalizer);
        }

        if (IsShutdownCompletionReentrantOnCurrentThread()) {
            return Task.CompletedTask;
        }

        return WaitForStopAsync(cancellationToken);
    }

    private StopRequest RequestStop(Action<Exception?>? shutdownFinalizer) {
        var wasCreated = false;
        Action<Exception?>? deferredFinalizer = null;
        lock (_gate) {
            if (shutdownFinalizer is not null) {
                if (!_shutdownFinalizersClaimed &&
                    _state != UiDispatcherState.Stopped) {
                    _shutdownFinalizers += shutdownFinalizer;
                } else {
                    deferredFinalizer = shutdownFinalizer;
                }
            }

            if (_state == UiDispatcherState.Created) {
                // No owner loop exists to drain an unstarted dispatcher.
                _state = UiDispatcherState.Stopping;
                wasCreated = true;
            } else if (_state == UiDispatcherState.Running) {
                _state = UiDispatcherState.Stopping;
            }
        }

        return new StopRequest(wasCreated, deferredFinalizer);
    }

    private bool TryBeginShutdownCompletion() {
        lock (_gate) {
            if (_state == UiDispatcherState.Stopped ||
                _shutdownCompletionStarted) {
                return false;
            }

            _shutdownCompletionStarted = true;
            _shutdownCompletionThreadId = Environment.CurrentManagedThreadId;
            return true;
        }
    }

    private bool IsShutdownCompletionReentrantOnCurrentThread() {
        lock (_gate) {
            return _shutdownCompletionStarted &&
                _shutdownCompletionThreadId == Environment.CurrentManagedThreadId &&
                !_shutdown.Task.IsCompleted;
        }
    }

    private Exception? CompleteStop() {
        Exception? failure = null;
        try {
            Drain();
        } catch (Exception exception) {
            failure = exception;
        }

        Action<Exception?>? finalizers;
        lock (_gate) {
            _state = UiDispatcherState.Stopped;
            _shutdownFailure = failure;
            _shutdownFinalizersClaimed = true;
            finalizers = _shutdownFinalizers;
            _shutdownFinalizers = null;
        }

        if (finalizers is not null) {
            foreach (var finalizer in finalizers.GetInvocationList()) {
                try {
                    ((Action<Exception?>)finalizer)(failure);
                } catch (Exception exception) {
                    failure = failure is null
                        ? exception
                        : new AggregateException(failure, exception);
                }
            }
        }

        lock (_gate) {
            _shutdownFailure = failure;
        }

        _shutdown.TrySetResult(failure);
        return failure;
    }

    private async Task WaitForStopAsync(CancellationToken cancellationToken) {
        var failure = await _shutdown.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void ScheduleDeferredFinalizer(Action<Exception?> finalizer) {
        _ = RunDeferredFinalizerAsync(finalizer);
    }

    private async Task RunDeferredFinalizerAsync(
        Action<Exception?> finalizer) {
        var failure = await _shutdown.Task.ConfigureAwait(false);
        try {
            finalizer(failure);
        } catch {
            // A finalizer that arrived after shutdown completion cannot affect
            // the dispatcher's already-published result. Its owner is
            // responsible for recording its own failure.
        }
    }

    /// <summary>
    /// Requests shutdown without surfacing shutdown failures. Use
    /// <see cref="DisposeAsync"/> when completion must be awaited.
    /// </summary>
    public void Dispose() {
        if (CheckAccess() ||
            State is UiDispatcherState.Created or UiDispatcherState.Stopped) {
            try {
                Stop();
            } catch {
                // ShutdownFailure remains available for callers that need it.
            }
        } else {
            _ = ObserveDisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task ObserveDisposeAsync() {
        try {
            await StopAsync().ConfigureAwait(false);
        } catch {
            // ShutdownFailure remains available for callers that need it.
        }
    }

    internal IDisposable InstallAsDefault() {
        var previous = Default;
        CurrentDefault.Value = this;
        return new UiDispatcherDefaultScope(this, previous);
    }

    internal void RestoreDefault(UiDispatcher? previous) {
        if (ReferenceEquals(Default, this)) {
            CurrentDefault.Value = previous;
        }
    }

    private readonly record struct StopRequest(
        bool WasCreated,
        Action<Exception?>? DeferredFinalizer);
}
