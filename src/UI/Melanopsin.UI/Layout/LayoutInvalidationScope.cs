namespace Melanopsin.UI.Layout;

/// <summary>
/// Describes how a layout pass attributed its invalidation work.
/// </summary>
public enum LayoutInvalidationScope {
    /// <summary>
    /// No layout pass has been recorded.
    /// </summary>
    None,

    /// <summary>
    /// No safe root-to-source path was available. The pass traverses from the
    /// root using the normal persistent-cache rules.
    /// </summary>
    Full,

    /// <summary>
    /// One or more layout-property changes were attributed to merged
    /// root-to-source paths. Traversal still enters at the root; unchanged
    /// siblings can reuse measurement and arrangement caches.
    /// </summary>
    MergedPaths,
}
