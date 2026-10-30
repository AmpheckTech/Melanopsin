using System.Runtime.ExceptionServices;
using Melanopsin.UI.Threading;

namespace Melanopsin.UI.App;

public sealed class UiRuntime : IDisposable, IAsyncDisposable {
    private readonly object _gate = new();
    private readonly TaskCompletionSource<Exception?> _stopCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<UiLifecycleState> _lifecycleNotifications = new();
    private UiLifecycleState _lifecycle = UiLifecycleState.Created;
    private Task<Exception?>? _stopTask;
    private Exception? _preStopFailure;
    private Exception? _stopFailure;
    private bool _stopFinalizationStarted;
    private int _stopFinalizationThreadId;
    private bool _lifecycleNotificationPumpActive;
    private bool _disposeRequested;
    private Action<UiLifecycleState>? _lifecycleChanged;

    public UiRuntime(
        UiDispatcher? dispatcher = null,
        ResourceResolver? resources = null) {
        Dispatcher = dispatcher ?? new UiDispatcher();
        Resources = resources ?? new ResourceResolver();
    }

    public event Action<UiLifecycleState>? LifecycleChanged {
        add {
            lock (_gate) {
                _lifecycleChanged += value;
            }
        }

        remove {
            lock (_gate) {
                _lifecycleChanged -= value;
            }
        }
    }

    public UiDispatcher Dispatcher { get; }

    public ResourceResolver Resources { get; }

    public UiLifecycleState Lifecycle {
        get {
            lock (_gate) {
                return _lifecycle;
            }
        }
    }

    /// <summary>
    /// The failure observed while shutting down or notifying lifecycle
    /// subscribers. Synchronous <see cref="Dispose"/> does not throw; use
    /// <see cref="StopAsync"/> or <see cref="DisposeAsync"/> to observe it.
    /// </summary>
    public Exception? LastStopFailure {
        get {
            lock (_gate) {
                return _stopFailure;
            }
        }
    }

    public bool IsDisposed {
        get {
            lock (_gate) {
                return _disposeRequested;
            }
        }
    }

    public void Start() {
        bool pumpNotifications;
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            if (_lifecycle == UiLifecycleState.Running) {
                return;
            }

            if (_lifecycle != UiLifecycleState.Created) {
                throw new InvalidOperationException(
                    $"A UI runtime in state '{_lifecycle}' cannot be started.");
            }

            // UiDispatcher.Start only performs an internal state transition and
            // does not invoke user code, so it is safe under the runtime gate.
            Dispatcher.Start();
            _lifecycle = UiLifecycleState.Running;
            pumpNotifications = EnqueueLifecycleNotificationLocked(
                UiLifecycleState.Running,
                canPump: true);
        }

        if (pumpNotifications) {
            PumpLifecycleNotifications();
        }
    }

    /// <summary>
    /// Requests shutdown and waits until shutdown has finalized. Prefer
    /// <see cref="StopAsync"/> from asynchronous code. An owner-thread reentrant
    /// call returns without blocking because that same thread owns shutdown
    /// finalization.
    /// </summary>
    public void Stop() {
        var stopTask = StopAsync();
        if (Dispatcher.CheckAccess() && !stopTask.IsCompleted) {
            return;
        }

        stopTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Requests shutdown. Cancellation abandons the wait, but does not cancel
    /// dispatcher shutdown. Repeated calls observe the same single-flight stop
    /// operation. A reentrant call from lifecycle finalization returns
    /// immediately so a subscriber cannot synchronously wait on itself.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Task<Exception?> completion;
        Exception? notificationFailure = null;
        bool initiateStop = false;
        bool continueStopOnOwner = false;
        bool pumpNotifications = false;
        var canPumpOnCaller = Dispatcher.CheckAccess() ||
            Dispatcher.State is UiDispatcherState.Created or UiDispatcherState.Stopped;

        lock (_gate) {
            if (_stopTask is null) {
                _lifecycle = UiLifecycleState.Stopping;
                _stopTask = _stopCompletion.Task;
                initiateStop = true;
                pumpNotifications = EnqueueLifecycleNotificationLocked(
                    UiLifecycleState.Stopping,
                    canPumpOnCaller);
            }

            completion = _stopTask;
        }

        if (pumpNotifications) {
            try {
                PumpLifecycleNotifications();
            } catch (Exception exception) {
                notificationFailure = exception;
            }
        }

        if (initiateStop) {
            BeginStop(notificationFailure);
        } else if (Dispatcher.CheckAccess() &&
                   Dispatcher.State != UiDispatcherState.Stopped) {
            continueStopOnOwner = true;
        }

        if (continueStopOnOwner) {
            ContinueStopOnOwner();
        }

        if (IsStopFinalizationReentrantOnCurrentThread()) {
            return Task.CompletedTask;
        }

        return WaitForStopAsync(completion, cancellationToken);
    }

    private void BeginStop(Exception? notificationFailure) {
        lock (_gate) {
            _preStopFailure = CombineFailures(
                _preStopFailure,
                notificationFailure);
        }

        try {
            var dispatcherStopTask = Dispatcher.StopAsync(
                FinalizeStop,
                CancellationToken.None);
            _ = ObserveDispatcherStopAsync(dispatcherStopTask);
        } catch (Exception exception) {
            FinalizeStop(exception);
        }
    }

    private void ContinueStopOnOwner() {
        try {
            var dispatcherStopTask = Dispatcher.StopAsync(CancellationToken.None);
            _ = ObserveDispatcherStopAsync(dispatcherStopTask);
        } catch (Exception exception) {
            FinalizeStop(exception);
        }
    }

    private async Task ObserveDispatcherStopAsync(Task dispatcherStopTask) {
        try {
            await dispatcherStopTask.ConfigureAwait(false);
            // Normally the dispatcher invokes FinalizeStop as its shutdown
            // finalizer. This fallback closes the race where dispatcher shutdown
            // completed before the runtime finalizer was registered.
            FinalizeStop(dispatcherFailure: null);
        } catch (Exception exception) {
            FinalizeStop(exception);
        }
    }

    private void FinalizeStop(Exception? dispatcherFailure) {
        Exception? preStopFailure;
        bool pumpNotifications;
        lock (_gate) {
            if (_stopFinalizationStarted) {
                return;
            }

            _stopFinalizationStarted = true;
            _stopFinalizationThreadId = Environment.CurrentManagedThreadId;
            _lifecycle = UiLifecycleState.Stopped;
            preStopFailure = _preStopFailure;
            pumpNotifications = EnqueueLifecycleNotificationLocked(
                UiLifecycleState.Stopped,
                canPump: true);
        }

        var failure = CombineFailures(preStopFailure, dispatcherFailure);
        if (pumpNotifications) {
            try {
                PumpLifecycleNotifications();
            } catch (Exception exception) {
                failure = CombineFailures(failure, exception);
            }
        }

        lock (_gate) {
            _stopFailure = failure;
        }

        _stopCompletion.TrySetResult(failure);
    }

    private bool IsStopFinalizationReentrantOnCurrentThread() {
        lock (_gate) {
            return _stopFinalizationStarted &&
                _stopFinalizationThreadId == Environment.CurrentManagedThreadId &&
                !_stopCompletion.Task.IsCompleted;
        }
    }

    private async Task WaitForStopAsync(
        Task<Exception?> completion,
        CancellationToken cancellationToken) {
        var failure = await completion
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private bool EnqueueLifecycleNotificationLocked(
        UiLifecycleState state,
        bool canPump) {
        _lifecycleNotifications.Enqueue(state);
        if (!canPump || _lifecycleNotificationPumpActive) {
            return false;
        }

        _lifecycleNotificationPumpActive = true;
        return true;
    }

    private void PumpLifecycleNotifications() {
        try {
            while (true) {
                UiLifecycleState state;
                Action<UiLifecycleState>? handlers;
                lock (_gate) {
                    if (_lifecycleNotifications.Count == 0) {
                        _lifecycleNotificationPumpActive = false;
                        return;
                    }

                    state = _lifecycleNotifications.Dequeue();
                    handlers = _lifecycleChanged;
                }

                Exception? failure = null;
                if (handlers is not null) {
                    foreach (var handler in handlers.GetInvocationList()) {
                        try {
                            ((Action<UiLifecycleState>)handler)(state);
                        } catch (Exception exception) {
                            failure = CombineFailures(failure, exception);
                        }
                    }
                }

                if (failure is not null) {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }
        } catch {
            lock (_gate) {
                _lifecycleNotifications.Clear();
                _lifecycleNotificationPumpActive = false;
            }

            throw;
        }
    }

    private static Exception? CombineFailures(
        Exception? first,
        Exception? second) {
        if (first is null) {
            return second;
        }

        if (second is null) {
            return first;
        }

        return first is AggregateException aggregate
            ? new AggregateException(aggregate.InnerExceptions.Append(second))
            : new AggregateException(first, second);
    }

    /// <summary>
    /// Requests shutdown without throwing shutdown failures. On a non-owner
    /// thread this returns after requesting shutdown; the owner thread completes
    /// finalization. Use <see cref="DisposeAsync"/> when completion must be
    /// awaited.
    /// </summary>
    public void Dispose() {
        if (!MarkDisposeRequested()) {
            return;
        }

        if (Dispatcher.CheckAccess() ||
            Dispatcher.State is UiDispatcherState.Created or UiDispatcherState.Stopped) {
            try {
                Stop();
            } catch {
                // LastStopFailure remains available for callers that need it.
            }
        } else {
            _ = ObserveDisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        MarkDisposeRequested();
        await StopAsync().ConfigureAwait(false);
    }

    private bool MarkDisposeRequested() {
        lock (_gate) {
            if (_disposeRequested) {
                return false;
            }

            _disposeRequested = true;
            return true;
        }
    }

    private async Task ObserveDisposeAsync() {
        try {
            await StopAsync().ConfigureAwait(false);
        } catch {
            // StopAsync stores the combined failure in LastStopFailure.
        }
    }
}
