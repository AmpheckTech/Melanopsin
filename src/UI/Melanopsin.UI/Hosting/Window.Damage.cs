using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    private void OnRootInvalidated(NodeInvalidation invalidation) {
        if (_disposed) {
            return;
        }

        var source = invalidation.Source;
        var kind = invalidation.Kind;
        if ((kind & InvalidationKind.Build) != 0) {
            _isDirty = true;
        }

        if ((kind & (InvalidationKind.Paint | InvalidationKind.Layout)) == 0) {
            return;
        }

        if ((kind & InvalidationKind.Layout) != 0) {
            if (invalidation.LayoutReason == LayoutInvalidationReason.LayoutProperty) {
                _invalidatedLayoutNodes.Add(source);
            } else {
                _forceFullLayoutInvalidation = true;
                _invalidatedLayoutNodes.Clear();
            }

            if (_lastLayout is { } currentLayout &&
                currentLayout.TryGetNode(source, out var currentLayoutNode)) {
                AddDamage(currentLayout, currentLayoutNode);
            }

            MarkLayoutDirty(
                invalidateLayoutCache: false,
                invalidatePaintCache: false,
                forceFullDamage: false);
            return;
        }

        var styleInvalidation = _renderContext.Styles.GetPendingStyleInvalidation(source);
        var stateNeedsLayout =
            (kind & InvalidationKind.Style) != 0 &&
            _renderContext.Styles.StateChangeRequiresLayout(source);
        if ((styleInvalidation & InvalidationKind.Layout) != 0 || stateNeedsLayout) {
            source.MarkLayoutChanged();
            _forceFullLayoutInvalidation = true;
            _invalidatedLayoutNodes.Clear();
            MarkLayoutDirty(
                invalidateLayoutCache: false,
                invalidatePaintCache: false,
                forceFullDamage: false);
            return;
        }

        _isDirty = true;
        if (_isLayoutDirty ||
            _lastLayout is null ||
            !_lastLayout.TryGetNode(source, out var layoutNode)) {
            _invalidatePaintCache = true;
            _pendingDamage.MarkFull();
            return;
        }

        var damageBounds = GetVisibleBounds(_lastLayout, layoutNode);
        if (!damageBounds.IsEmpty) {
            _pendingDamage.Add(damageBounds.Inflate(1));
        }
    }

    private void CaptureLayoutGeometry(LayoutSnapshot layout) {
        _previousLayoutGeometry.Clear();
        foreach (var node in layout.Nodes) {
            if (!layout.TryGetNode(node, out var layoutNode)) {
                continue;
            }

            _previousLayoutGeometry[node] = new LayoutNodeGeometry(
                layoutNode.Bounds,
                GetVisibleBounds(layout, layoutNode),
                layoutNode.ClipBounds,
                layoutNode.Parent,
                layoutNode.SiblingIndex);
        }
    }

    private void AddLayoutDamage(LayoutSnapshot layout) {
        foreach (var previous in _previousLayoutGeometry) {
            if (!layout.Contains(previous.Key)) {
                AddDamage(previous.Value.VisibleBounds);
            }
        }

        foreach (var node in layout.Nodes) {
            if (!layout.TryGetNode(node, out var layoutNode)) {
                continue;
            }

            var current = new LayoutNodeGeometry(
                layoutNode.Bounds,
                GetVisibleBounds(layout, layoutNode),
                layoutNode.ClipBounds,
                layoutNode.Parent,
                layoutNode.SiblingIndex);
            if (!_previousLayoutGeometry.TryGetValue(node, out var previous)) {
                AddDamage(current.VisibleBounds);
                continue;
            }

            if (previous.Bounds != current.Bounds ||
                previous.VisibleBounds != current.VisibleBounds ||
                previous.ClipBounds != current.ClipBounds ||
                !ReferenceEquals(previous.Parent, current.Parent) ||
                previous.SiblingIndex != current.SiblingIndex) {
                AddDamage(previous.VisibleBounds);
                AddDamage(current.VisibleBounds);
            }
        }

        _previousLayoutGeometry.Clear();
    }

    private void AddDamage(LayoutSnapshot layout, LayoutNode layoutNode) {
        AddDamage(GetVisibleBounds(layout, layoutNode));
    }

    private void AddDamage(Rect bounds) {
        if (!bounds.IsEmpty) {
            _pendingDamage.Add(bounds.Inflate(1));
        }
    }

    private static Rect GetVisibleBounds(
        LayoutSnapshot layout,
        LayoutNode layoutNode) {
        var bounds = ToWindowBounds(layoutNode);
        for (var ancestor = layoutNode.Parent;
             ancestor is not null;
             ancestor = ancestor.Parent) {
            if (ancestor.Node is Control { ClipToBounds: true }) {
                bounds = bounds.Intersect(ToWindowBounds(ancestor));
            }
        }

        return bounds;
    }

    private static Rect ToWindowBounds(LayoutNode layoutNode) {
        var x = layoutNode.Bounds.MinX;
        var y = layoutNode.Bounds.MinY;
        for (var ancestor = layoutNode.Parent;
             ancestor is not null;
             ancestor = ancestor.Parent) {
            if (ancestor.Node is IScrollTransform scrollTransform) {
                x -= scrollTransform.OffsetX;
                y -= scrollTransform.OffsetY;
            }
        }

        return new Rect(
            x,
            y,
            layoutNode.Bounds.Width,
            layoutNode.Bounds.Height);
    }

    private readonly record struct LayoutNodeGeometry(
        Rect Bounds,
        Rect VisibleBounds,
        Rect? ClipBounds,
        LayoutNode? Parent,
        int SiblingIndex);
}
