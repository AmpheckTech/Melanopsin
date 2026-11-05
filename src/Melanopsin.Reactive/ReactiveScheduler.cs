// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// Installs the ambient scheduler captured by reactive sources. When no
/// scheduler is installed, notifications are delivered synchronously.
/// </summary>
public static class ReactiveScheduler {
    private static readonly AsyncLocal<IReactiveScheduler?> CurrentDefault = new();

    public static IReactiveScheduler? Default => CurrentDefault.Value;

    public static IDisposable Push(IReactiveScheduler scheduler) {
        ArgumentNullException.ThrowIfNull(scheduler);
        var previous = Default;
        CurrentDefault.Value = scheduler;
        return new ReactiveSchedulerScope(scheduler, previous);
    }

    private sealed class ReactiveSchedulerScope : IDisposable {
        private readonly IReactiveScheduler _scheduler;
        private IReactiveScheduler? _previous;
        private int _disposed;

        public ReactiveSchedulerScope(
            IReactiveScheduler scheduler,
            IReactiveScheduler? previous) {
            _scheduler = scheduler;
            _previous = previous;
        }

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) {
                return;
            }

            if (ReferenceEquals(ReactiveScheduler.Default, _scheduler)) {
                CurrentDefault.Value = _previous;
            }

            _previous = null;
        }
    }
}
