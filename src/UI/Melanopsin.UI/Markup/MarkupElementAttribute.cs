namespace Melanopsin.UI.Markup;

/// <summary>
/// Marks a <see cref="UI.Element"/> type as a markup element.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MarkupElementAttribute : Attribute {
    public MarkupElementAttribute()
        : this(null) {
    }

    public MarkupElementAttribute(string? name) {
        Name = name;
    }

    public string? Name { get; }
}
