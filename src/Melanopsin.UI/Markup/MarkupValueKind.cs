namespace Melanopsin.UI.Markup;

/// <summary>
/// Describes the compile-time validation applied to a static markup attribute value.
/// </summary>
public enum MarkupValueKind {
    Any,
    Number,
    Length,
    Integer,
    Boolean,
    Enum,
    Input,
}
