using Melanopsin.Reactive;

namespace Melanopsin.UI;

internal sealed class ElementBinding<T> : ElementBinding, IReactiveInvalidationTarget {
    private readonly Func<T> _getter;
    private readonly Action<Element, T> _apply;
    private HashSet<IReactiveSource> _subscribed = [];
    private Element _target;
    private T _lastValue = default!;
    private bool _hasValue;
    private bool _isActive;

    public ElementBinding(
        Element target,
        Func<T> getter,
        Action<Element, T> apply) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(apply);
        _target = target;
        _getter = getter;
        _apply = apply;
        Refresh();
    }

    public override void Retarget(Element target) {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        _lastValue = default!;
        _hasValue = false;
    }

    public override void Activate() {
        if (_isActive) {
            return;
        }

        _isActive = true;
        Refresh();
    }

    public override void Deactivate() {
        if (!_isActive) {
            return;
        }

        _isActive = false;
        foreach (var source in _subscribed) {
            source.Invalidated -= OnSourceInvalidated;
        }

        _subscribed.Clear();
    }

    void IReactiveInvalidationTarget.InvalidateFromReactiveSource() {
        if (_isActive) {
            Refresh();
        }
    }

    private void Refresh() {
        var dependencies = new HashSet<IReactiveSource>();
        var tentativeSubscriptions = new HashSet<IReactiveSource>();
        T value;
        try {
            using (ReactiveRuntime.Track(
                dependencies,
                source => SubscribeDuringRefresh(source, tentativeSubscriptions))) {
                value = _getter();
            }

            if (!_hasValue || !EqualityComparer<T>.Default.Equals(_lastValue, value)) {
                _lastValue = value;
                _hasValue = true;
                _apply(_target, value);
            }

            UpdateDependencies(dependencies, tentativeSubscriptions);
        } catch {
            Unsubscribe(tentativeSubscriptions);
            throw;
        }
    }

    private void SubscribeDuringRefresh(
        IReactiveSource source,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        if (!_isActive || _subscribed.Contains(source) || !tentativeSubscriptions.Add(source)) {
            return;
        }

        source.Invalidated += OnSourceInvalidated;
        _subscribed.Add(source);
    }

    private void UpdateDependencies(
        HashSet<IReactiveSource> dependencies,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        foreach (var source in _subscribed) {
            if (!dependencies.Contains(source)) {
                source.Invalidated -= OnSourceInvalidated;
            }
        }

        _subscribed.RemoveWhere(source => !dependencies.Contains(source));
        if (_isActive) {
            foreach (var source in dependencies) {
                if (!_subscribed.Contains(source) && !tentativeSubscriptions.Contains(source)) {
                    source.Invalidated += OnSourceInvalidated;
                    _subscribed.Add(source);
                }
            }
        }
    }

    private void Unsubscribe(HashSet<IReactiveSource> subscriptions) {
        foreach (var source in subscriptions) {
            source.Invalidated -= OnSourceInvalidated;
            _subscribed.Remove(source);
        }
    }

    private void OnSourceInvalidated(IReactiveSource source) {
        ReactiveRuntime.RequestInvalidation(this);
    }
}
