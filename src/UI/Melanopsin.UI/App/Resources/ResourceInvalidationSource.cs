// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.ExceptionServices;
using Melanopsin.Reactive;

namespace Melanopsin.UI.App;

internal sealed class ResourceInvalidationSource : IReactiveSource {
    private readonly object _gate = new();
    private event Action<IReactiveSource>? SourceInvalidated;

    public void Invalidate() {
        Action<IReactiveSource>? handlers;
        lock (_gate) {
            handlers = SourceInvalidated;
        }

        if (handlers is null) {
            return;
        }

        Exception? failure = null;
        var notification = ReactiveRuntime.BeginNotification();
        try {
            foreach (var handler in handlers.GetInvocationList()) {
                try {
                    ((Action<IReactiveSource>)handler)(this);
                } catch (Exception exception) {
                    failure = Combine(failure, exception);
                }
            }
        } finally {
            try {
                notification.Dispose();
            } catch (Exception exception) {
                failure = Combine(failure, exception);
            }
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    event Action<IReactiveSource>? IReactiveSource.Invalidated {
        add {
            lock (_gate) {
                SourceInvalidated += value;
            }
        }

        remove {
            lock (_gate) {
                SourceInvalidated -= value;
            }
        }
    }

    private static Exception Combine(Exception? first, Exception second) {
        return first is null
            ? second
            : new AggregateException(first, second);
    }
}
