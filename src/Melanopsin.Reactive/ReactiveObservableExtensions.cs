// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// Bridges pull-based signals to the BCL <see cref="IObservable{T}"/> contract.
/// Use System.Reactive when event-stream operators such as throttling,
/// windows, or time-based sampling are required.
/// </summary>
public static class ReactiveObservableExtensions {
    /// <summary>
    /// Creates a push stream that emits the current value and every subsequent
    /// change. The source is read inside an effect, so derived values are
    /// recomputed only while at least one observer is active.
    /// </summary>
    public static IObservable<T> AsObservable<T>(
        this IReadOnlySignal<T> source,
        bool emitInitial = true,
        bool distinctUntilChanged = false) {
        ArgumentNullException.ThrowIfNull(source);
        return new SignalObservable<T>(
            source,
            emitInitial,
            distinctUntilChanged);
    }

    private sealed class SignalObservable<T> : IObservable<T> {
        private readonly IReadOnlySignal<T> _source;
        private readonly bool _emitInitial;
        private readonly bool _distinctUntilChanged;

        public SignalObservable(
            IReadOnlySignal<T> source,
            bool emitInitial,
            bool distinctUntilChanged) {
            _source = source;
            _emitInitial = emitInitial;
            _distinctUntilChanged = distinctUntilChanged;
        }

        public IDisposable Subscribe(IObserver<T> observer) {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new SignalSubscription<T>(
                _source,
                observer,
                _emitInitial,
                _distinctUntilChanged);
            subscription.Start();
            return subscription;
        }
    }

    private sealed class SignalSubscription<T> : IDisposable {
        private readonly object _gate = new();
        private readonly IReadOnlySignal<T> _source;
        private readonly IObserver<T> _observer;
        private readonly bool _emitInitial;
        private readonly bool _distinctUntilChanged;
        private readonly IEqualityComparer<T> _comparer =
            EqualityComparer<T>.Default;
        private Effect? _effect;
        private T _lastValue = default!;
        private bool _hasValue;
        private bool _hasRun;
        private int _terminated;

        public SignalSubscription(
            IReadOnlySignal<T> source,
            IObserver<T> observer,
            bool emitInitial,
            bool distinctUntilChanged) {
            _source = source;
            _observer = observer;
            _emitInitial = emitInitial;
            _distinctUntilChanged = distinctUntilChanged;
        }

        public void Start() {
            var effect = new Effect(Evaluate);
            lock (_gate) {
                if (Volatile.Read(ref _terminated) == 0) {
                    _effect = effect;
                    return;
                }
            }

            effect.Dispose();
        }

        public void Dispose() {
            Terminate();
        }

        private void Evaluate() {
            if (Volatile.Read(ref _terminated) != 0) {
                return;
            }

            T value;
            try {
                value = _source.Value;
            } catch (Exception exception) {
                Fail(exception);
                return;
            }

            if (!_hasRun) {
                _hasRun = true;
                if (!_emitInitial) {
                    return;
                }
            }

            if (_distinctUntilChanged && _hasValue &&
                _comparer.Equals(_lastValue, value)) {
                return;
            }

            _lastValue = value;
            _hasValue = true;
            try {
                ReactiveRuntime.Untrack(() => _observer.OnNext(value));
            } catch (Exception exception) {
                Fail(exception);
            }
        }

        private void Fail(Exception exception) {
            if (Interlocked.Exchange(ref _terminated, 1) != 0) {
                return;
            }

            Effect? effect;
            lock (_gate) {
                effect = _effect;
                _effect = null;
            }

            effect?.Dispose();
            ReactiveRuntime.Untrack(() => _observer.OnError(exception));
        }

        private void Terminate() {
            if (Interlocked.Exchange(ref _terminated, 1) != 0) {
                return;
            }

            Effect? effect;
            lock (_gate) {
                effect = _effect;
                _effect = null;
            }

            effect?.Dispose();
        }
    }
}
