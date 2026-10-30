using System.Collections.ObjectModel;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

internal sealed class ElementState {
    private readonly Dictionary<string, object?> _attributes =
        new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, object?> _attributesView;
    private List<ElementBinding>? _bindings;

    public ElementState() {
        _attributesView = new ReadOnlyDictionary<string, object?>(_attributes);
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributesView;

    public InlineStyle InlineStyle { get; } = new();

    public IEnumerable<string> AttributeNames => _attributes.Keys;

    public bool TrySetAttribute(string name, object? value) {
        if (_attributes.TryGetValue(name, out var current) &&
            Equals(current, value)) {
            return false;
        }

        _attributes[name] = value;
        return true;
    }

    public bool RemoveAttribute(string name) {
        return _attributes.Remove(name);
    }

    public void AddBinding(ElementBinding binding) {
        (_bindings ??= []).Add(binding);
    }

    public void SetBindingsActive(bool active) {
        if (_bindings is null) {
            return;
        }

        foreach (var binding in _bindings) {
            if (active) {
                binding.Activate();
            } else {
                binding.Deactivate();
            }
        }
    }

    public void ReplaceBindingsFrom(
        ElementState source,
        Element target,
        bool active) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        SetBindingsActive(active: false);
        source.SetBindingsActive(active: false);
        if (source._bindings is null) {
            return;
        }

        _bindings = source._bindings;
        source._bindings = null;
        foreach (var binding in _bindings) {
            binding.Retarget(target);
        }

        SetBindingsActive(active);
    }
}
