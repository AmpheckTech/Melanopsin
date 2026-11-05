// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// Observable value that participates in automatic dependency tracking. Writes
/// notify synchronously on the scheduler thread and are posted to the captured
/// scheduler otherwise.
/// </summary>
public sealed class Signal<T> : IWritableSignal<T>, IReactiveSource {
    private readonly object _gate = new();
    private readonly IEqualityComparer<T> _comparer;
    private T _value;
    private Action<T>? _changed;
    private Action<IReactiveSource>? _sourceInvalidated;
    private IReactiveScheduler? _scheduler;
    private long _revision;

    public Signal(T initialValue)
        : this(initialValue, null) {
    }

    /// <param name="initialValue">The initial signal value.</param>
    /// <param name="comparer">Suppresses writes that do not change the value; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
    public Signal(
        T initialValue,
        IEqualityComparer<T>? comparer) {
        _value = initialValue;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public event Action<T>? Changed {
        add {
            lock (_gate) {
                _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
                _changed += value;
            }
        }

        remove {
            lock (_gate) {
                _changed -= value;
            }
        }
    }

    public T Value {
        get {
            ReactiveRuntime.Track(this);
            lock (_gate) {
                _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
                return _value;
            }
        }

        set {
            long revision;
            IReactiveScheduler? scheduler;
            lock (_gate) {
                if (_comparer.Equals(_value, value)) {
                    return;
                }

                _value = value;
                revision = ++_revision;
                _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
                scheduler = _scheduler;
            }

            _ = ReactiveNotificationDispatcher.TryInvoke(
                scheduler,
                () => NotifyInvalidated(revision));
        }
    }

    /// <summary>
    /// Applies an update as one atomic signal operation and returns whether the
    /// value changed. The callback runs while the signal is write-locked; it
    /// must not read or write the same signal.
    /// </summary>
    public bool Update(Func<T, T> update) {
        ArgumentNullException.ThrowIfNull(update);
        long revision;
        IReactiveScheduler? scheduler;
        lock (_gate) {
            var value = update(_value);
            if (_comparer.Equals(_value, value)) {
                return false;
            }

            _value = value;
            revision = ++_revision;
            _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
            scheduler = _scheduler;
        }

        _ = ReactiveNotificationDispatcher.TryInvoke(
            scheduler,
            () => NotifyInvalidated(revision));
        return true;
    }

    /// <summary>
    /// Notifies consumers even when the value itself has not changed.
    /// </summary>
    public void Invalidate() {
        long revision;
        IReactiveScheduler? scheduler;
        lock (_gate) {
            revision = ++_revision;
            _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
            scheduler = _scheduler;
        }

        _ = ReactiveNotificationDispatcher.TryInvoke(
            scheduler,
            () => NotifyInvalidated(revision));
    }

    private void NotifyInvalidated(long revision) {
        T value;
        Action<T>? changed;
        Action<IReactiveSource>? invalidated;
        lock (_gate) {
            if (_revision != revision) {
                return;
            }

            value = _value;
            changed = _changed;
            invalidated = _sourceInvalidated;
        }

        Exception? failure = null;
        var notification = ReactiveRuntime.BeginNotification();
        try {
            failure = ReactiveEventInvoker.Combine(
                failure,
                ReactiveEventInvoker.Invoke(changed, value));
            failure = ReactiveEventInvoker.Combine(
                failure,
                ReactiveEventInvoker.Invoke(invalidated, this));
        } finally {
            try {
                notification.Dispose();
            } catch (Exception exception) {
                failure = ReactiveEventInvoker.Combine(failure, exception);
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }

    public T Peek() {
        lock (_gate) {
            _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
            return _value;
        }
    }

    public override string? ToString() {
        lock (_gate) {
            return _value?.ToString();
        }
    }

    event Action<IReactiveSource>? IReactiveSource.Invalidated {
        add {
            lock (_gate) {
                _scheduler = ReactiveNotificationDispatcher.CaptureOrReuse(_scheduler);
                _sourceInvalidated += value;
            }
        }

        remove {
            lock (_gate) {
                _sourceInvalidated -= value;
            }
        }
    }
}
