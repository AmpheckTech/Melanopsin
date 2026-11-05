// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Reactive;

namespace Melanopsin.UI;

internal sealed class ContextValue<T> : IReactiveSource {
    private readonly Signal<T> _signal;

    public ContextValue(T value) {
        _signal = new Signal<T>(value);
    }

    public T GetValue() {
        return _signal.Value;
    }

    public T Peek() {
        return _signal.Peek();
    }

    public void SetValue(T value) {
        _signal.Value = value;
    }

    public void Invalidate() {
        _signal.Invalidate();
    }

    event Action<IReactiveSource>? IReactiveSource.Invalidated {
        add => ((IReactiveSource)_signal).Invalidated += value;
        remove => ((IReactiveSource)_signal).Invalidated -= value;
    }
}
