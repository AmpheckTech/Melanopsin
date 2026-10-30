namespace Melanopsin.UI;

/// <summary>
/// Describes one pipeline invalidation raised by a node.
/// </summary>
public readonly record struct NodeInvalidation(
    Node Source,
    InvalidationKind Kind,
    LayoutInvalidationReason LayoutReason);
