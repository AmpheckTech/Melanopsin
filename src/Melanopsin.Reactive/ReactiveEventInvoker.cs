using System.Runtime.ExceptionServices;

namespace Melanopsin.Reactive;

internal static class ReactiveEventInvoker {
    public static Exception? Invoke<T>(Action<T>? handlers, T argument) {
        if (handlers is null) {
            return null;
        }

        Exception? failure = null;
        foreach (var handler in handlers.GetInvocationList()) {
            try {
                ((Action<T>)handler)(argument);
            } catch (Exception exception) {
                failure = Combine(failure, exception);
            }
        }

        return failure;
    }

    public static Exception? InvokeAll<T>(
        IEnumerable<T> items,
        Action<T> action) {
        Exception? failure = null;
        foreach (var item in items) {
            try {
                action(item);
            } catch (Exception exception) {
                failure = Combine(failure, exception);
            }
        }

        return failure;
    }

    public static Exception? Combine(Exception? first, Exception? second) {
        if (first is null) {
            return second;
        }

        if (second is null) {
            return first;
        }

        return first is AggregateException aggregate
            ? new AggregateException(aggregate.InnerExceptions.Append(second))
            : new AggregateException(first, second);
    }

    public static void ThrowIfNotNull(Exception? failure) {
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
