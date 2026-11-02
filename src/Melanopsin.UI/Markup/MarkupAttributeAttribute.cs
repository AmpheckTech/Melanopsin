namespace Melanopsin.UI.Markup;

/// <summary>
/// Declares a markup attribute supported by an element or one of its properties.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = true)]
public sealed class MarkupAttributeAttribute : Attribute {
    public MarkupAttributeAttribute(string name)
        : this(name, MarkupValueKind.Any) {
    }

    public MarkupAttributeAttribute(string name, MarkupValueKind valueKind) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ValueKind = valueKind;
    }

    public string Name { get; }

    public MarkupValueKind ValueKind { get; }
}
