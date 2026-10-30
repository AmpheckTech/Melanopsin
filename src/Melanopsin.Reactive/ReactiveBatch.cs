namespace Melanopsin.Reactive;

/// <summary>
/// Groups multiple synchronous reactive writes so each affected invalidation
/// target is notified at most once when the batch completes.
/// </summary>
public static class ReactiveBatch {
    public static void Run(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        Exception? failure = null;
        var notification = ReactiveRuntime.BeginNotification();
        try {
            action();
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                notification.Dispose();
            } catch (Exception exception) {
                failure = ReactiveEventInvoker.Combine(failure, exception);
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }

    public static T Run<T>(Func<T> action) {
        ArgumentNullException.ThrowIfNull(action);
        T result = default!;
        Exception? failure = null;
        var notification = ReactiveRuntime.BeginNotification();
        try {
            result = action();
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                notification.Dispose();
            } catch (Exception exception) {
                failure = ReactiveEventInvoker.Combine(failure, exception);
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
        return result;
    }
}
