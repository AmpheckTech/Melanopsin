namespace Melanopsin.Reactive;

/// <summary>
/// Lazily evaluated derived value that records the reactive sources its factory
/// reads.
/// </summary>
public sealed class Computed<T> : IReadOnlySignal<T>, IReactiveSource {
    private readonly object _gate = new();
    private readonly object _computeGate = new();
    private readonly Func<T> _compute;
    private HashSet<IReactiveSource> _dependencies = [];
    private event Action<IReactiveSource>? SourceInvalidated;
    private T _value = default!;
    private bool _hasValue;
    private bool _dirty = true;
    private long _invalidationVersion;

    public Computed(Func<T> compute) {
        ArgumentNullException.ThrowIfNull(compute);
        _compute = compute;
    }

    public T Value {
        get {
            ReactiveRuntime.Track(this);
            return GetValue();
        }
    }

    public T Peek() {
        return GetValue();
    }

    public override string? ToString() {
        return Value?.ToString();
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

    private T GetValue() {
        lock (_gate) {
            if (_hasValue && !_dirty) {
                return _value;
            }
        }

        lock (_computeGate) {
            while (true) {
                long invalidationVersion;
                lock (_gate) {
                    if (_hasValue && !_dirty) {
                        return _value;
                    }

                    invalidationVersion = _invalidationVersion;
                }

                var dependencies = new HashSet<IReactiveSource>();
                var tentativeSubscriptions = new HashSet<IReactiveSource>();
                T value;
                ReactiveRuntime.EnterComputation(this);
                try {
                    try {
                        using (ReactiveRuntime.Track(
                            dependencies,
                            source => SubscribeDuringComputation(
                                source,
                                tentativeSubscriptions))) {
                            value = _compute();
                        }
                    } catch {
                        Unsubscribe(tentativeSubscriptions);
                        throw;
                    }
                } finally {
                    ReactiveRuntime.ExitComputation(this);
                }

                HashSet<IReactiveSource> previousDependencies;
                lock (_gate) {
                    previousDependencies = _dependencies;
                    _dependencies = dependencies;
                    _value = value;
                    _hasValue = true;
                    _dirty = true;
                }

                // Event subscription changes are external calls. Keep them out
                // of _gate so custom sources cannot deadlock the computed.
                foreach (var source in previousDependencies) {
                    if (!dependencies.Contains(source)) {
                        source.Invalidated -= OnDependencyInvalidated;
                    }
                }

                lock (_gate) {
                    if (_invalidationVersion == invalidationVersion) {
                        _dirty = false;
                        return _value;
                    }

                    // A source changed while the factory was running. Keep the
                    // cached value dirty and compute again from the latest state.
                    _dirty = true;
                }
            }
        }
    }

    private void SubscribeDuringComputation(
        IReactiveSource source,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        lock (_gate) {
            if (_dependencies.Contains(source) || !tentativeSubscriptions.Add(source)) {
                return;
            }
        }

        source.Invalidated += OnDependencyInvalidated;
    }

    private void Unsubscribe(HashSet<IReactiveSource> subscriptions) {
        foreach (var source in subscriptions) {
            source.Invalidated -= OnDependencyInvalidated;
        }
    }

    private void OnDependencyInvalidated(IReactiveSource source) {
        Action<IReactiveSource>? handlers = null;
        lock (_gate) {
            _invalidationVersion++;
            if (_dirty) {
                return;
            }

            _dirty = true;
            handlers = SourceInvalidated;
        }

        if (handlers is not null) {
            Exception? failure = null;
            var notification = ReactiveRuntime.BeginNotification();
            try {
                failure = ReactiveEventInvoker.Invoke(handlers, this);
            } finally {
                try {
                    notification.Dispose();
                } catch (Exception exception) {
                    failure = ReactiveEventInvoker.Combine(failure, exception);
                }
            }

            ReactiveEventInvoker.ThrowIfNotNull(failure);
        }
    }
}
