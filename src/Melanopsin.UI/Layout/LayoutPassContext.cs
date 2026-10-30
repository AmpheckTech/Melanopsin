namespace Melanopsin.UI.Layout;

/// <summary>
/// Describes the work requested for one layout pass.
/// </summary>
public readonly record struct LayoutPassContext {
    public LayoutPassContext(
        bool invalidateCache = false,
        bool viewportChanged = false,
        IReadOnlyCollection<Node>? invalidatedLayoutNodes = null,
        bool forceFullInvalidation = false) {
        InvalidateCache = invalidateCache;
        ViewportChanged = viewportChanged;
        InvalidatedLayoutNodes = invalidatedLayoutNodes;
        ForceFullInvalidation = forceFullInvalidation;
    }

    /// <summary>
    /// Clears persistent layout caches before measuring and arranging.
    /// </summary>
    public bool InvalidateCache { get; }

    /// <summary>
    /// Whether the layout viewport changed for this pass. Viewport-relative
    /// measurements are re-evaluated while viewport-independent measurements
    /// remain eligible for persistent-cache reuse.
    /// </summary>
    public bool ViewportChanged { get; }

    /// <summary>
    /// Layout nodes that changed before this pass. Multiple nodes may share
    /// one pass; the layout engine attributes them to merged root-to-source paths.
    /// The paths are diagnostics, not a promise to bypass necessary parent
    /// measurement or arrange work.
    /// </summary>
    public IReadOnlyCollection<Node>? InvalidatedLayoutNodes { get; }

    /// <summary>
    /// Requests full layout invalidation even when invalidated nodes are present. This
    /// is used when a change cannot be attributed to a safe ancestor path.
    /// </summary>
    public bool ForceFullInvalidation { get; }

    public bool IsFullInvalidation =>
        InvalidateCache ||
        ForceFullInvalidation ||
        (!ViewportChanged &&
            InvalidatedLayoutNodes is not { Count: > 0 });
}
