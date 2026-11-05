// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Markup;

namespace Melanopsin.UI;

/// <summary>
/// Runtime metadata for one typed element attribute. Controls expose these as
/// static fields so generated code can avoid string-based dispatch while the
/// public string API remains available to diagnostics and dynamic tooling.
/// </summary>
public abstract class ElementAttribute {
    protected ElementAttribute(
        string name,
        MarkupValueKind valueKind,
        InvalidationKind invalidation,
        bool affectsStyle) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ValueKind = valueKind;
        Invalidation = invalidation;
        AffectsStyle = affectsStyle;
    }

    public string Name { get; }

    public MarkupValueKind ValueKind { get; }

    public InvalidationKind Invalidation { get; }

    public bool AffectsStyle { get; }

    internal abstract void SetValue(Element element, object? value);
}

/// <summary>Typed attribute metadata declared by an element type.</summary>
public sealed class ElementAttribute<T> : ElementAttribute {
    private readonly Action<Element, T> _setter;
    private readonly Action<Element> _reset;

    public ElementAttribute(
        string name,
        MarkupValueKind valueKind,
        Action<Element, T> setter,
        Action<Element> reset,
        InvalidationKind invalidation = InvalidationKind.LayoutAndPaint,
        bool affectsStyle = false)
        : base(name, valueKind, invalidation, affectsStyle) {
        ArgumentNullException.ThrowIfNull(setter);
        ArgumentNullException.ThrowIfNull(reset);
        _setter = setter;
        _reset = reset;
    }

    public void SetValue(Element element, T value) {
        ArgumentNullException.ThrowIfNull(element);
        _setter(element, value);
    }

    public void ResetValue(Element element) {
        ArgumentNullException.ThrowIfNull(element);
        _reset(element);
    }

    internal override void SetValue(Element element, object? value) {
        if (value is null) {
            _reset(element);
            return;
        }

        if (value is not T typedValue) {
            throw new ArgumentException(
                $"Attribute '{Name}' expects a value of type '{typeof(T).FullName}'.",
                nameof(value));
        }

        _setter(element, typedValue);
    }
}
