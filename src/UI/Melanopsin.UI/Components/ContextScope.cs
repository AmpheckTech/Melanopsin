namespace Melanopsin.UI;

public sealed class ContextScope<T> : IDisposable {
    private readonly IContextStore _store;
    private readonly Context<T> _context;
    private readonly ContextValue<T> _binding;
    private readonly bool _hadPrevious;
    private readonly T? _previousValue;
    private int _disposed;

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

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        if (_hadPrevious) {
            _binding.SetValue(_previousValue!);
            return;
        }

        _binding.Invalidate();
        _store.ContextValues?.Remove(_context);
    }
}
