namespace Melanopsin.UI;

public sealed class ContextScope<T> : IDisposable {
    private readonly IContextStore _store;
    private readonly Context<T> _context;
    private readonly ContextValue<T> _binding;
    private readonly bool _hadPrevious;
    private readonly T? _previousValue;
    private int _disposed;
    private Exception? _disposeFailure;

    internal ContextScope(
        IContextStore store,
        Context<T> context,
        ContextValue<T> binding,
        bool hadPrevious,
        T? previousValue) {
        _store = store;
        _context = context;
        _binding = binding;
        _hadPrevious = hadPrevious;
        _previousValue = previousValue;
    }

    public T Value {
        get => _binding.GetValue();
        set => _binding.SetValue(value);
    }

    /// <summary>
    /// The failure observed during the most recent disposal. Disposal records
    /// this value instead of throwing so it cannot mask an active exception.
    /// </summary>
    public Exception? LastDisposeFailure => Volatile.Read(ref _disposeFailure);

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        Exception? failure = null;
        if (_hadPrevious) {
            CaptureFailure(
                ref failure,
                () => _binding.SetValue(_previousValue!));
        } else {
            CaptureFailure(ref failure, _binding.Invalidate);
            CaptureFailure(
                ref failure,
                () => _store.ContextValues?.Remove(_context));
        }

        Volatile.Write(ref _disposeFailure, failure);
    }

    private static void CaptureFailure(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception exception) {
            failure = failure is null
                ? exception
                : new AggregateException(failure, exception);
        }
    }
}
