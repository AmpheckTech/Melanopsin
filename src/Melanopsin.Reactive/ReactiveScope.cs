// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// Owns reactive resources and disposes them in reverse creation order.
/// Effects created while the scope is current are registered automatically.
/// </summary>
public sealed class ReactiveScope : IDisposable {
    private static readonly AsyncLocal<ReactiveScope?> CurrentScope = new();
    private readonly object _gate = new();
    private readonly ReactiveScope? _parent;
    private List<IDisposable>? _resources = [];
    private bool _disposed;

    private ReactiveScope(ReactiveScope? parent) {
        _parent = parent;
    }

    /// <summary>
    /// Gets the scope that owns reactive resources created in the current
    /// execution context.
    /// </summary>
    public static ReactiveScope? Current => CurrentScope.Value;

    /// <summary>
    /// Creates and enters a new scope. Dispose it with <c>using</c> to end the
    /// scope and release every resource it owns.
    /// </summary>
    public static ReactiveScope Create() {
        var parent = Current;
        var scope = new ReactiveScope(parent);
        parent?.Add(scope);
        if (!scope.IsDisposed) {
            CurrentScope.Value = scope;
        }

        return scope;
    }

    private bool IsDisposed {
        get {
            lock (_gate) {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Adds a disposable resource to this scope. If the scope is already
    /// disposed, the resource is disposed immediately.
    /// </summary>
    public T Add<T>(T resource)
        where T : IDisposable {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_gate) {
            if (!_disposed) {
                _resources!.Add(resource);
                return resource;
            }
        }

        resource.Dispose();
        return resource;
    }

    /// <summary>
    /// Adds an action to run when this scope is disposed.
    /// </summary>
    public IDisposable Add(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        return Add(new ActionDisposable(action));
    }

    public void Dispose() {
        List<IDisposable>? resources;
        lock (_gate) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            resources = _resources;
            _resources = null;
        }

        if (IsSameOrDescendant(CurrentScope.Value, this)) {
            CurrentScope.Value = _parent;
        }

        Exception? failure = null;
        for (var index = resources!.Count - 1; index >= 0; index--) {
            try {
                resources[index].Dispose();
            } catch (Exception exception) {
                failure = ReactiveEventInvoker.Combine(failure, exception);
            }
        }

        ReactiveEventInvoker.ThrowIfNotNull(failure);
    }

    private static bool IsSameOrDescendant(
        ReactiveScope? scope,
        ReactiveScope ancestor) {
        while (scope is not null) {
            if (ReferenceEquals(scope, ancestor)) {
                return true;
            }

            scope = scope._parent;
        }

        return false;
    }

    private sealed class ActionDisposable : IDisposable {
        private Action? _action;

        public ActionDisposable(Action action) {
            _action = action;
        }

        public void Dispose() {
            Interlocked.Exchange(ref _action, null)?.Invoke();
        }
    }
}
