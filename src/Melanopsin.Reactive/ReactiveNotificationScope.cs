// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

internal sealed class ReactiveNotificationScope : IDisposable {
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private int _disposed;

    public void Dispose() {
        if (Environment.CurrentManagedThreadId != _ownerThreadId) {
            throw new InvalidOperationException(
                "A reactive notification scope must be disposed on the thread that created it.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        ReactiveRuntime.NotificationDepth--;
        if (ReactiveRuntime.NotificationDepth != 0) {
            return;
        }

        var pendingInvalidations = ReactiveRuntime.PendingInvalidations;
        ReactiveRuntime.PendingInvalidations = null;
        if (pendingInvalidations is null) {
            return;
        }

        var failure = ReactiveEventInvoker.InvokeAll(
            pendingInvalidations,
            static target => target.InvalidateFromReactiveSource());
        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }
}
