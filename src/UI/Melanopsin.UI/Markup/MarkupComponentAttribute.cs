namespace Melanopsin.UI.Markup;

/// <summary>
/// Marks a <see cref="Component"/> as usable from markup by its class name or
/// the optional tag name supplied to this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MarkupComponentAttribute : Attribute {
    public MarkupComponentAttribute()
        : this(null) {
    }

    public MarkupComponentAttribute(string? name) {
        Name = name;
    }

    public string? Name { get; }
}
