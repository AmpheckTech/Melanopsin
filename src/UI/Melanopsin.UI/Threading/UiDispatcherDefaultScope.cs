// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Threading;

internal sealed class UiDispatcherDefaultScope : IDisposable {
    private readonly UiDispatcher _dispatcher;
    private readonly UiDispatcher? _previous;
    private int _disposed;

    public UiDispatcherDefaultScope(
        UiDispatcher dispatcher,
        UiDispatcher? previous) {
        _dispatcher = dispatcher;
        _previous = previous;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        _dispatcher.RestoreDefault(_previous);
    }
}
