namespace Melanopsin.Reactive;

/// <summary>
/// Runs an action immediately and repeats it whenever one of the reactive
/// sources read by the action changes. Dependencies are replaced after every
/// successful run, so conditional reads are supported.
/// </summary>
public sealed class Effect : IReactiveInvalidationTarget, IDisposable {
    private const int MaximumConsecutiveRuns = 100;
    private readonly object _gate = new();
    private readonly Action _run;
    private HashSet<IReactiveSource> _dependencies = [];
    private bool _disposed;
    private bool _running;
    private bool _pending;

    /// <summary>
    /// Creates an effect, runs it once synchronously, and registers it with the
    /// current <see cref="ReactiveScope"/> when one exists.
    /// </summary>
    public Effect(Action run) {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
        try {
            Run();
        } catch {
            Dispose();
            throw;
        }

        ReactiveScope.Current?.Add(this);
    }

    void IReactiveInvalidationTarget.InvalidateFromReactiveSource() {
        Run();
    }

    public void Dispose() {
        HashSet<IReactiveSource>? dependencies;
        lock (_gate) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _pending = false;
            dependencies = _dependencies;
            _dependencies = [];
        }

        Unsubscribe(dependencies);
    }

    public override string ToString() {
        return $"Effect {{ Dependencies = {_dependencies.Count} }}";
    }

    private void Run() {
        lock (_gate) {
            if (_disposed) {
                return;
            }

            if (_running) {
                _pending = true;
                return;
            }

            _running = true;
        }

        Exception? failure = null;
        var runCount = 0;
        try {
            while (true) {
                lock (_gate) {
                    if (_disposed) {
                        return;
                    }

                    _pending = false;
                }

                RunOnce();
                runCount++;
                if (runCount > MaximumConsecutiveRuns) {
                    throw new InvalidOperationException(
                        "The effect did not stabilize after " +
                        $"{MaximumConsecutiveRuns} consecutive runs. " +
                        "A reactive update cycle is likely.");
                }

                lock (_gate) {
                    if (_disposed || !_pending) {
                        return;
                    }
                }
            }
        } catch (Exception exception) {
            failure = exception;
        } finally {
            lock (_gate) {
                _running = false;
                _pending = false;
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }

    private void RunOnce() {
        var dependencies = new HashSet<IReactiveSource>();
        var tentativeSubscriptions = new HashSet<IReactiveSource>();
        try {
            using (ReactiveRuntime.Track(
                dependencies,
                source => SubscribeDuringRun(source, tentativeSubscriptions))) {
                ReactiveBatch.Run(_run);
            }
        } catch {
            Unsubscribe(tentativeSubscriptions);
            throw;
        }

        ReplaceDependencies(dependencies, tentativeSubscriptions);
    }

    private void SubscribeDuringRun(
        IReactiveSource source,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        lock (_gate) {
            if (_disposed ||
                _dependencies.Contains(source) ||
                !tentativeSubscriptions.Add(source)) {
                return;
            }
        }

        source.Invalidated += OnDependencyInvalidated;
    }

    private void ReplaceDependencies(
        HashSet<IReactiveSource> dependencies,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        HashSet<IReactiveSource>? removed = null;
        HashSet<IReactiveSource>? abandonedTentative = null;
        lock (_gate) {
            if (_disposed) {
                abandonedTentative = tentativeSubscriptions;
            } else {
                removed = [];
                foreach (var source in _dependencies) {
                    if (!dependencies.Contains(source)) {
                        removed.Add(source);
                    }
                }

                _dependencies = dependencies;
            }
        }

        if (removed is not null) {
            Unsubscribe(removed);
        }
        if (abandonedTentative is not null) {
            Unsubscribe(abandonedTentative);
        }
    }

    private void Unsubscribe(IEnumerable<IReactiveSource> sources) {
        Exception? failure = null;
        foreach (var source in sources) {
            try {
                source.Invalidated -= OnDependencyInvalidated;
            } catch (Exception exception) {
                failure = ReactiveEventInvoker.Combine(failure, exception);
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }

    private void OnDependencyInvalidated(IReactiveSource source) {
        ReactiveRuntime.RequestInvalidation(this);
    }
}
