namespace Melanopsin.UI.Threading;

public sealed partial class UiDispatcher {
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
