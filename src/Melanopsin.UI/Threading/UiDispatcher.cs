using System.Collections.Concurrent;
using Melanopsin.Reactive;

namespace Melanopsin.UI.Threading;

public sealed partial class UiDispatcher : IDisposable, IAsyncDisposable, IReactiveScheduler {
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

}
