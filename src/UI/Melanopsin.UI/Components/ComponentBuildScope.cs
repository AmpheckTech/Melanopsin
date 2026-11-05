namespace Melanopsin.UI;

internal static class ComponentBuildScope {
    [ThreadStatic]
    private static Component? _current;

    public static Component? Current => _current;

    public static IDisposable Enter(Component component) {
        ArgumentNullException.ThrowIfNull(component);
        var previous = _current;
        _current = component;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable {
        private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
        private Component? _previous;
        private int _disposed;

        public Scope(Component? previous) {
            _previous = previous;
        }

        public void Dispose() {
            if (Environment.CurrentManagedThreadId != _ownerThreadId) {
                throw new InvalidOperationException(
                    "A component build scope must be disposed on the thread that created it.");
            }

            if (Interlocked.Exchange(ref _disposed, 1) != 0) {
                return;
            }

            _current = _previous;
            _previous = null;
        }
    }
}
