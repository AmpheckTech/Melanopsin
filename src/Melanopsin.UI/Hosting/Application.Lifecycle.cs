using System.Runtime.ExceptionServices;
using Melanopsin.Reactive;

namespace Melanopsin.UI.Hosting;

public sealed partial class Application {
    public void Dispose() {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) {
            return;
        }

        _windows.WindowCreated -= OnWindowCreated;
        Exception? failure = null;
        try {
            _runtime.Dispose();
            failure = _runtime.LastStopFailure;
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                RestoreDispatcherDefault();
            } catch (Exception exception) {
                failure = CombineFailures(failure, exception);
            }
        }

        Volatile.Write(ref _cleanupFailure, failure);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) {
            return;
        }

        _windows.WindowCreated -= OnWindowCreated;
        Exception? failure = null;
        try {
            await _runtime.DisposeAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                RestoreDispatcherDefault();
            } catch (Exception exception) {
                failure = CombineFailures(failure, exception);
            }
        }

        Volatile.Write(ref _cleanupFailure, failure);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Exception CombineFailures(
        Exception? first,
        Exception second) {
        return first is null
            ? second
            : new AggregateException(first, second);
    }

    private void RestoreDispatcherDefault() {
        _reactiveSchedulerScope?.Dispose();
        _reactiveSchedulerScope = null;
        _dispatcherScope?.Dispose();
        _dispatcherScope = null;
    }
}
