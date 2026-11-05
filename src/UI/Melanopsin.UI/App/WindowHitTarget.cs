namespace Melanopsin.UI.App;

/// <summary>
/// Immutable snapshot of a hit-tested node, its window bounds and capture route.
/// The route is copied so later layout passes cannot mutate a consumed result.
/// </summary>
public sealed class WindowHitTarget {
    internal WindowHitTarget(
        Node node,
        Rect bounds,
        IReadOnlyList<Node> route) {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(route);
        Node = node;
        Bounds = bounds;
        Route = Array.AsReadOnly(route.ToArray());
    }

    public Node Node { get; }

    public Rect Bounds { get; }

    public IReadOnlyList<Node> Route { get; }

    internal bool Contains(float x, float y) {
        return Bounds.Contains(ToLayoutX(x), ToLayoutY(y));
    }

    internal float ToLayoutX(float x) {
        return x + GetAncestorScrollOffsetX();
    }

    internal float ToLayoutY(float y) {
        return y + GetAncestorScrollOffsetY();
    }

    internal float ToLayoutX(float x, Node currentTarget) {
        return x + GetScrollOffsetX(currentTarget);
    }

    internal float ToLayoutY(float y, Node currentTarget) {
        return y + GetScrollOffsetY(currentTarget);
    }

    private float GetAncestorScrollOffsetX() {
        var offsetX = 0f;
        foreach (var node in Route) {
            if (ReferenceEquals(node, Node)) {
                break;
            }

            if (node is IScrollTransform scrollTransform) {
                offsetX += scrollTransform.OffsetX;
            }
        }

        return offsetX;
    }

    private float GetAncestorScrollOffsetY() {
        var offsetY = 0f;
        foreach (var node in Route) {
            if (ReferenceEquals(node, Node)) {
                break;
            }

            if (node is IScrollTransform scrollTransform) {
                offsetY += scrollTransform.OffsetY;
            }
        }

        return offsetY;
    }

    private float GetScrollOffsetX(Node currentTarget) {
        var offsetX = 0f;
        foreach (var node in Route) {
            if (ReferenceEquals(node, currentTarget)) {
                break;
            }

            if (node is IScrollTransform scrollTransform) {
                offsetX += scrollTransform.OffsetX;
            }
        }

        return offsetX;
    }

    private float GetScrollOffsetY(Node currentTarget) {
        var offsetY = 0f;
        foreach (var node in Route) {
            if (ReferenceEquals(node, currentTarget)) {
                break;
            }

            if (node is IScrollTransform scrollTransform) {
                offsetY += scrollTransform.OffsetY;
            }
        }

        return offsetY;
    }
}
