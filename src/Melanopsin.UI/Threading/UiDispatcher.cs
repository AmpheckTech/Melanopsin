using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Melanopsin.Reactive;

namespace Melanopsin.UI.Threading;

public sealed class UiDispatcher : IDisposable, IAsyncDisposable, IReactiveScheduler {
    private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(30);
    private static readonly AsyncLocal<UiDispatcher?> CurrentDefault = new();

    private readonly ConcurrentQueue<WorkItem> _pending = new();
    private readonly object _gate = new();
    private readonly int _threadId;
    private readonly TaskCompletionSource<Exception?> _shutdown =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private UiDispatcherState _state = UiDispatcherState.Created;
    private Exception? _shutdownFailure;
    private bool _shutdownCompletionStarted;
    private int _shutdownCompletionThreadId;
    private Action<Exception?>? _shutdownFinalizers;
    private bool _shutdownFinalizersClaimed;

    public UiDispatcher() {
        _threadId = Environment.CurrentManagedThreadId;
    }

    public static UiDispatcher? Default => CurrentDefault.Value;

    public UiDispatcherState State {
        get {
            lock (_gate) {
                return _state;
            }
        }
    }

    /// <summary>
    /// The failure observed while draining and finalizing the dispatcher, if any.
    /// Synchronous <see cref="Dispose"/> does not throw; use <see cref="StopAsync"/>
    /// or <see cref="DisposeAsync"/> when the failure must be observed.
    /// </summary>
    public Exception? ShutdownFailure {
        get {
            lock (_gate) {
                return _shutdownFailure;
            }
        }
    }

    public bool IsRunning => State == UiDispatcherState.Running;

    public bool IsStopped => State is UiDispatcherState.Stopping or UiDispatcherState.Stopped;

    public bool HasPendingWork => !_pending.IsEmpty;

    public void Start() {
        lock (_gate) {
            ThrowIfStopped();
            if (_state == UiDispatcherState.Running) {
                return;
            }

            _state = UiDispatcherState.Running;
        }
    }

    public bool CheckAccess() {
        return Environment.CurrentManagedThreadId == _threadId;
    }

    public void Post(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate) {
            ThrowIfNotRunning();
            _pending.Enqueue(WorkItem.Posted(action));
        }
    }

    /// <summary>
    /// Enqueues work when the dispatcher is running. Returns
    /// <see langword="false"/> before startup and after shutdown instead of
    /// throwing.
    /// </summary>
    public bool TryPost(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate) {
            if (_state != UiDispatcherState.Running) {
                return false;
            }

            _pending.Enqueue(WorkItem.Posted(action));
            return true;
        }
    }

    /// <summary>
    /// Executes work synchronously on the dispatcher owner thread. Calling this
    /// from another thread can deadlock when the owner is waiting for that same
    /// caller; prefer <see cref="Post"/> from asynchronous or cross-thread paths.
    /// </summary>
    public void Send(Action action) {
        Send(action, DefaultSendTimeout);
    }

    public void Send(Action action, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (CheckAccess()) {
            lock (_gate) {
                ThrowIfNotRunning();
            }

            action();
            return;
        }

        var work = WorkItem.Sent(action);
        Enqueue(work);
        try {
            work.Wait(cancellationToken);
        } catch (OperationCanceledException) {
            work.Abandon();
            throw;
        }

        ThrowFailure(work);
    }

    public void Send(Action action, TimeSpan timeout) {
        ArgumentNullException.ThrowIfNull(action);
        ValidateTimeout(timeout);
        if (CheckAccess()) {
            lock (_gate) {
                ThrowIfNotRunning();
            }

            action();
            return;
        }

        var work = WorkItem.Sent(action);
        Enqueue(work);
        if (!work.Wait(timeout)) {
            work.Abandon();
            throw new TimeoutException(
                $"The UI dispatcher did not process work within '{timeout}'.");
        }

        ThrowFailure(work);
    }

    public int Drain(int maximumItems = int.MaxValue) {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumItems);
        ThrowIfNotOwnerThread();

        var processed = 0;
        List<Exception>? failures = null;
        while (processed < maximumItems && _pending.TryDequeue(out var work)) {
            try {
                work.Execute();
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            }

            processed++;
        }

        if (failures is not null) {
            throw new AggregateException(
                "One or more UI dispatcher work items failed.",
                failures);
        }

        return processed;
    }

    /// <summary>
    /// Requests shutdown and waits until the owner thread has drained queued
    /// work and finalized the dispatcher. Prefer <see cref="StopAsync"/> from
    /// asynchronous code. An owner-thread reentrant call returns without waiting
    /// because the dispatcher is already being finalized by that thread.
    /// </summary>
    public void Stop() {
        var stopTask = StopAsync();
        if (CheckAccess() && !stopTask.IsCompleted) {
            return;
        }

        stopTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Requests shutdown. Owner-thread calls finalize synchronously; other
    /// threads wait asynchronously for the owner to drain and finalize the
    /// dispatcher. Cancellation abandons the wait, but does not cancel shutdown.
    /// A reentrant call from shutdown finalization returns immediately so a
    /// subscriber cannot deadlock by synchronously waiting for its own callback.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default) {
        return StopAsyncCore(shutdownFinalizer: null, cancellationToken);
    }

    internal Task StopAsync(
        Action<Exception?> shutdownFinalizer,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(shutdownFinalizer);
        return StopAsyncCore(shutdownFinalizer, cancellationToken);
    }

    private Task StopAsyncCore(
        Action<Exception?>? shutdownFinalizer,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var request = RequestStop(shutdownFinalizer);
        if ((request.WasCreated || CheckAccess()) &&
            TryBeginShutdownCompletion()) {
            var failure = CompleteStop();
            return failure is null
                ? Task.CompletedTask
                : Task.FromException(failure);
        }

        if (request.DeferredFinalizer is not null) {
            ScheduleDeferredFinalizer(request.DeferredFinalizer);
        }

        if (IsShutdownCompletionReentrantOnCurrentThread()) {
            return Task.CompletedTask;
        }

        return WaitForStopAsync(cancellationToken);
    }

    private StopRequest RequestStop(Action<Exception?>? shutdownFinalizer) {
        var wasCreated = false;
        Action<Exception?>? deferredFinalizer = null;
        lock (_gate) {
            if (shutdownFinalizer is not null) {
                if (!_shutdownFinalizersClaimed &&
                    _state != UiDispatcherState.Stopped) {
                    _shutdownFinalizers += shutdownFinalizer;
                } else {
                    deferredFinalizer = shutdownFinalizer;
                }
            }

            if (_state == UiDispatcherState.Created) {
                // No owner loop exists to drain an unstarted dispatcher.
                _state = UiDispatcherState.Stopping;
                wasCreated = true;
            } else if (_state == UiDispatcherState.Running) {
                _state = UiDispatcherState.Stopping;
            }
        }

        return new StopRequest(wasCreated, deferredFinalizer);
    }

    private bool TryBeginShutdownCompletion() {
        lock (_gate) {
            if (_state == UiDispatcherState.Stopped ||
                _shutdownCompletionStarted) {
                return false;
            }

            _shutdownCompletionStarted = true;
            _shutdownCompletionThreadId = Environment.CurrentManagedThreadId;
            return true;
        }
    }

    private bool IsShutdownCompletionReentrantOnCurrentThread() {
        lock (_gate) {
            return _shutdownCompletionStarted &&
                _shutdownCompletionThreadId == Environment.CurrentManagedThreadId &&
                !_shutdown.Task.IsCompleted;
        }
    }

    private Exception? CompleteStop() {
        Exception? failure = null;
        try {
            Drain();
        } catch (Exception exception) {
            failure = exception;
        }

        Action<Exception?>? finalizers;
        lock (_gate) {
            _state = UiDispatcherState.Stopped;
            _shutdownFailure = failure;
            _shutdownFinalizersClaimed = true;
            finalizers = _shutdownFinalizers;
            _shutdownFinalizers = null;
        }

        if (finalizers is not null) {
            foreach (var finalizer in finalizers.GetInvocationList()) {
                try {
                    ((Action<Exception?>)finalizer)(failure);
                } catch (Exception exception) {
                    failure = failure is null
                        ? exception
                        : new AggregateException(failure, exception);
                }
            }
        }

        lock (_gate) {
            _shutdownFailure = failure;
        }

        _shutdown.TrySetResult(failure);
        return failure;
    }

    private async Task WaitForStopAsync(CancellationToken cancellationToken) {
        var failure = await _shutdown.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void ScheduleDeferredFinalizer(Action<Exception?> finalizer) {
        _ = RunDeferredFinalizerAsync(finalizer);
    }

    private async Task RunDeferredFinalizerAsync(
        Action<Exception?> finalizer) {
        var failure = await _shutdown.Task.ConfigureAwait(false);
        try {
            finalizer(failure);
        } catch {
            // A finalizer that arrived after shutdown completion cannot affect
            // the dispatcher's already-published result. Its owner is
            // responsible for recording its own failure.
        }
    }

    /// <summary>
    /// Requests shutdown without surfacing shutdown failures. On a non-owner
    /// thread this returns after requesting shutdown; the owner thread completes
    /// finalization. Use <see cref="DisposeAsync"/> when completion must be
    /// awaited.
    /// </summary>
    public void Dispose() {
        if (CheckAccess() ||
            State is UiDispatcherState.Created or UiDispatcherState.Stopped) {
            try {
                Stop();
            } catch {
                // ShutdownFailure remains available for callers that need it.
            }
        } else {
            _ = ObserveDisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task ObserveDisposeAsync() {
        try {
            await StopAsync().ConfigureAwait(false);
        } catch {
            // ShutdownFailure remains available for callers that need it.
        }
    }

    internal IDisposable InstallAsDefault() {
        var previous = Default;
        CurrentDefault.Value = this;
        return new UiDispatcherDefaultScope(this, previous);
    }

    internal void RestoreDefault(UiDispatcher? previous) {
        if (ReferenceEquals(Default, this)) {
            CurrentDefault.Value = previous;
        }
    }

    private void Enqueue(WorkItem work) {
        lock (_gate) {
            ThrowIfNotRunning();
            _pending.Enqueue(work);
        }
    }

    private static void ThrowFailure(WorkItem work) {
        if (work.Failure is not null) {
            throw new AggregateException(
                "UI dispatcher work failed.",
                work.Failure);
        }
    }

    private void ThrowIfStopped() {
        if (_state is UiDispatcherState.Stopping or UiDispatcherState.Stopped) {
            throw new ObjectDisposedException(
                nameof(UiDispatcher),
                "The UI dispatcher has stopped.");
        }
    }

    private void ThrowIfNotOwnerThread() {
        if (!CheckAccess()) {
            throw new InvalidOperationException(
                "UI dispatcher work can only be drained on the dispatcher's owner thread.");
        }
    }

    private void ThrowIfNotRunning() {
        ThrowIfStopped();
        if (_state != UiDispatcherState.Running) {
            throw new InvalidOperationException(
                "The UI dispatcher has not been started.");
        }
    }

    private static void ValidateTimeout(TimeSpan timeout) {
        if (timeout < Timeout.InfiniteTimeSpan) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The timeout must be non-negative or Timeout.InfiniteTimeSpan.");
        }
    }

    private readonly record struct StopRequest(
        bool WasCreated,
        Action<Exception?>? DeferredFinalizer);

    private sealed class WorkItem {
        private readonly Action _action;
        private readonly TaskCompletionSource<Exception?>? _completion;
        private int _abandoned;

        private WorkItem(Action action, TaskCompletionSource<Exception?>? completion) {
            _action = action;
            _completion = completion;
        }

        public Exception? Failure { get; private set; }

        public static WorkItem Posted(Action action) {
            return new WorkItem(action, completion: null);
        }

        public static WorkItem Sent(Action action) {
            return new WorkItem(
                action,
                new TaskCompletionSource<Exception?>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public void Execute() {
            if (_completion is null) {
                _action();
                return;
            }

            if (Volatile.Read(ref _abandoned) != 0) {
                return;
            }

            try {
                _action();
            } catch (Exception exception) {
                Failure = exception;
            } finally {
                _completion.TrySetResult(Failure);
            }
        }

        public void Abandon() {
            Interlocked.Exchange(ref _abandoned, 1);
            _completion?.TrySetResult(null);
        }

        public void Wait(CancellationToken cancellationToken) {
            _completion!.Task.Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout) {
            return _completion!.Task.Wait(timeout);
        }
    }
}
