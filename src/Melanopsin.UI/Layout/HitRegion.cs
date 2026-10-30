using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

public sealed class HitRegion {
    public HitRegion(
        Node node,
        Rect bounds,
        IReadOnlyList<Node> route,
        Rect? clipBounds = null) {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(route);
        Node = node;
        Bounds = bounds;
        Route = route;
        ClipBounds = clipBounds;
    }

    public Node Node { get; }

    public Rect Bounds { get; internal set; }

    public IReadOnlyList<Node> Route { get; internal set; }

    public Rect? ClipBounds { get; internal set; }

    internal void Update(
        Rect bounds,
        IReadOnlyList<Node> route,
        Rect? clipBounds) {
        Bounds = bounds;
        Route = route;
        ClipBounds = clipBounds;
    }

    internal bool Contains(
        float x,
        float y,
        LayoutSnapshot layout) {
        ArgumentNullException.ThrowIfNull(layout);
        var offsetX = 0f;
        var offsetY = 0f;
        foreach (var node in Route) {
            if (node is not Control { ClipToBounds: true } ||
                !layout.TryGetNode(node, out var layoutNode)) {
                continue;
            }

            var clipBounds = new Rect(
                layoutNode.Bounds.X - offsetX,
                layoutNode.Bounds.Y - offsetY,
                layoutNode.Bounds.Width,
                layoutNode.Bounds.Height);
            if (!clipBounds.Contains(x, y)) {
                return false;
            }

            if (!ReferenceEquals(node, Node) &&
                node is IScrollTransform scrollTransform) {
                offsetX += scrollTransform.OffsetX;
                offsetY += scrollTransform.OffsetY;
            }
        }

        return Bounds.Contains(x + offsetX, y + offsetY);
    }
}
