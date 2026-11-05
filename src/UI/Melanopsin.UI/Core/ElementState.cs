using System.Collections.ObjectModel;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

internal sealed class ElementState {
    private static readonly IReadOnlyDictionary<string, object?> NoAttributes =
        new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal));

    private readonly Element _owner;
    private Dictionary<string, object?>? _attributes;
    private IReadOnlyDictionary<string, object?>? _attributesView;
    private ElementBinding? _binding;
    private List<ElementBinding>? _bindings;
    private InlineStyle? _inlineStyle;

    public ElementState(Element owner) {
        _owner = owner;
    }

    public IReadOnlyDictionary<string, object?> Attributes =>
        _attributesView ?? NoAttributes;

    public bool HasInlineStyle => _inlineStyle is not null;

    public InlineStyle InlineStyle => _inlineStyle ??= CreateInlineStyle();

    public IEnumerable<string> AttributeNames =>
        _attributes is not null ? _attributes.Keys : Array.Empty<string>();

    public bool TrySetAttribute(string name, object? value) {
        if (_attributes is not null &&
            _attributes.TryGetValue(name, out var current) &&
            Equals(current, value)) {
            return false;
        }

        var attributes = _attributes;
        if (attributes is null) {
            attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
            _attributes = attributes;
            _attributesView = new ReadOnlyDictionary<string, object?>(attributes);
        }

        attributes[name] = value;
        return true;
    }

    public bool RemoveAttribute(string name) =>
        _attributes is not null && _attributes.Remove(name);

    /// <summary>
    /// Applies an attribute to the inline style, allocating the style only when
    /// the name is one the inline style owns.
    /// </summary>
    public bool TrySetInlineStyle(string name, object? value) {
        return Melanopsin.UI.Styling.InlineStyle.IsProperty(name) &&
            InlineStyle.TrySet(name, value);
    }

    /// <summary>
    /// Copies inline style values from a rebuilt element, skipping the copy
    /// entirely when neither side carries inline styles.
    /// </summary>
    public bool CopyInlineStyleFrom(ElementState source) {
        if (_inlineStyle is null && source._inlineStyle is null) {
            return false;
        }

        return InlineStyle.CopyFrom(source._inlineStyle);
    }

    public void AddBinding(ElementBinding binding) {
        if (_binding is null) {
            _binding = binding;
            return;
        }

        var bindings = _bindings ??= [_binding];
        bindings.Add(binding);
        _binding = null;
    }

    public void SetBindingsActive(bool active) {
        var single = _binding;
        if (single is not null) {
            SetBindingActive(single, active);
        }

        var bindings = _bindings;
        if (bindings is null) {
            return;
        }

        foreach (var binding in bindings) {
            SetBindingActive(binding, active);
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
        _binding = source._binding;
        _bindings = source._bindings;
        source._binding = null;
        source._bindings = null;
        if (_binding is not null) {
            _binding.Retarget(target);
        }

        if (_bindings is not null) {
            foreach (var binding in _bindings) {
                binding.Retarget(target);
            }
        }

        SetBindingsActive(active);
    }

    private static void SetBindingActive(ElementBinding binding, bool active) {
        if (active) {
            binding.Activate();
        } else {
            binding.Deactivate();
        }
    }

    private InlineStyle CreateInlineStyle() {
        var style = new InlineStyle();
        style.Changed += _owner.OnInlineStyleChanged;
        return style;
    }
}
