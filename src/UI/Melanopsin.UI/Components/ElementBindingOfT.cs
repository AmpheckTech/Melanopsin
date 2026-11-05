using Melanopsin.Reactive;

namespace Melanopsin.UI;

internal sealed class ElementBinding<T> : ElementBinding, IReactiveInvalidationTarget {
    private readonly Func<T> _getter;
    private readonly Action<Element, T>? _apply;
    private readonly string? _attributeName;
    private readonly ElementAttribute<T>? _attribute;
    private HashSet<IReactiveSource>? _subscribed;
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

    /// <summary>
    /// Creates a binding that writes a named attribute. The name lives on the
    /// binding so generated code does not allocate a capturing delegate per
    /// element.
    /// </summary>
    public ElementBinding(Element target, string attributeName, Func<T> getter) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentNullException.ThrowIfNull(getter);
        _target = target;
        _getter = getter;
        _attributeName = attributeName;
        Refresh();
    }

    public ElementBinding(
        Element target,
        ElementAttribute<T> attribute,
        Func<T> getter) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(getter);
        _target = target;
        _getter = getter;
        _attribute = attribute;
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
        var subscribed = _subscribed;
        if (subscribed is null) {
            return;
        }

        foreach (var source in subscribed) {
            source.Invalidated -= OnSourceInvalidated;
        }

        subscribed.Clear();
    }

    void IReactiveInvalidationTarget.InvalidateFromReactiveSource() {
        if (_isActive) {
            Refresh();
        }
    }

    private void Refresh() {
        if (!_isActive) {
            // Element bindings are created while building an unmounted tree, so
            // the first read has nothing to subscribe to yet. Reading untracked
            // keeps element construction free of tracking bookkeeping.
            ApplyValue(ReactiveRuntime.Untrack(_getter));
            return;
        }

        var dependencies = new HashSet<IReactiveSource>();
        try {
            using (ReactiveRuntime.Track(dependencies, SubscribeDuringRefresh)) {
                ApplyValue(_getter());
            }
        } catch {
            UnsubscribeAll();
            throw;
        }

        DropStaleSubscriptions(dependencies);
    }

    private void ApplyValue(T value) {
        if (!_hasValue || !EqualityComparer<T>.Default.Equals(_lastValue, value)) {
            _lastValue = value;
            _hasValue = true;
            if (_attribute is { } attribute) {
                _target.SetAttributeValue(attribute, value);
            } else if (_attributeName is { } name) {
                _target.SetAttributeValue(name, value);
            } else {
                _apply!(_target, value);
            }
        }
    }

    private void SubscribeDuringRefresh(IReactiveSource source) {
        var subscribed = _subscribed ??= [];
        if (!subscribed.Add(source)) {
            return;
        }

        source.Invalidated += OnSourceInvalidated;
    }

    private void DropStaleSubscriptions(HashSet<IReactiveSource> dependencies) {
        var subscribed = _subscribed;
        if (subscribed is null || subscribed.Count == 0) {
            return;
        }

        List<IReactiveSource>? stale = null;
        foreach (var source in subscribed) {
            if (!dependencies.Contains(source)) {
                (stale ??= []).Add(source);
            }
        }

        if (stale is null) {
            return;
        }

        foreach (var source in stale) {
            source.Invalidated -= OnSourceInvalidated;
            subscribed.Remove(source);
        }
    }

    /// <summary>
    /// Drops every subscription after a failed read. The binding re-establishes
    /// its dependencies on the next successful refresh.
    /// </summary>
    private void UnsubscribeAll() {
        var subscribed = _subscribed;
        if (subscribed is null) {
            return;
        }

        foreach (var source in subscribed) {
            source.Invalidated -= OnSourceInvalidated;
        }

        subscribed.Clear();
    }

    private void OnSourceInvalidated(IReactiveSource source) {
        ReactiveRuntime.RequestInvalidation(this);
    }
}
