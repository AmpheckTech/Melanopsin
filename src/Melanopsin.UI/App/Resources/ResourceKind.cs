namespace Melanopsin.UI.App;

/// <summary>
/// Describes the semantic contract of a resource. The value type alone is not
/// enough because future pack formats need to distinguish messages from plain
/// strings and image bytes from arbitrary binary data.
/// </summary>
public enum ResourceKind {
    String,
    Message,
    Image,
    Font,
    Color,
    Length,
    Number,
    Binary,
    Object,
}
