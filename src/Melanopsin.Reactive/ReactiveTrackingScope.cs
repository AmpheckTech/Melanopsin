// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

internal sealed class ReactiveTrackingScope : IDisposable {
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private HashSet<IReactiveSource>? _previous;
    private Action<IReactiveSource>? _previousSourceTracked;
    private int _disposed;

    public ReactiveTrackingScope(
        HashSet<IReactiveSource>? previous,
        Action<IReactiveSource>? previousSourceTracked) {
        _previous = previous;
        _previousSourceTracked = previousSourceTracked;
    }

    public void Dispose() {
        if (Environment.CurrentManagedThreadId != _ownerThreadId) {
            throw new InvalidOperationException(
                "A reactive tracking scope must be disposed on the thread that created it.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        ReactiveRuntime.Dependencies = _previous;
        ReactiveRuntime.SourceTracked = _previousSourceTracked;
        _previous = null;
        _previousSourceTracked = null;
    }
}
