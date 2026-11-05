using Melanopsin.UI;

namespace Melanopsin.UI.Rendering;

public sealed partial class DefaultRenderSceneComposer {
    private readonly Dictionary<Node, BoundaryLayerCache> _boundaryCaches = [];

    private RenderLayer AppendBoundary(
        LayoutNode layoutNode,
        RenderOptions options,
        bool invalidatePaintCache,
        bool cacheRaster,
        ref int reusedBoundaries,
        ref int repaintedBoundaries) {
        var node = layoutNode.Node;
        if (!invalidatePaintCache &&
            _boundaryCaches.TryGetValue(node, out var cache) &&
            cache.IsValid(layoutNode, options)) {
            reusedBoundaries++;
            return cache.Layer;
        }

        repaintedBoundaries++;
        var contentLayer = AppendNodeContent(
            layoutNode,
            options,
            invalidatePaintCache,
            ref reusedBoundaries,
            ref repaintedBoundaries);
        // A filter samples within its subtree, while a backdrop filter samples
        // behind it. Retaining either output can capture stale surrounding
        // pixels, including when a blurred node is a descendant of this
        // boundary.
        cacheRaster &= !ContainsBlurLayer(contentLayer);
        var layer = cacheRaster
            ? new RetainedLayer(layoutNode.Bounds, contentLayer)
            : contentLayer;
        _boundaryCaches[node] = new BoundaryLayerCache(
            node.SubtreePaintRevision,
            layoutNode.Bounds,
            layoutNode.ClipBounds,
            options.DrawDebugBounds,
            layer);
        return layer;
    }

    private static bool ContainsBlurLayer(RenderLayer layer) {
        return layer switch {
            BlurLayer or BackdropBlurLayer => true,
            ContainerLayer container => container.Children.Any(ContainsBlurLayer),
            ClipLayer clip => ContainsBlurLayer(clip.Child),
            TransformLayer transform => ContainsBlurLayer(transform.Child),
            RetainedLayer retained => ContainsBlurLayer(retained.Child),
            _ => false,
        };
    }

    private void RemoveStaleCaches(LayoutSnapshot layout) {
        if (_boundaryCaches.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in _boundaryCaches.Keys) {
            if (layout.Contains(node)) {
                continue;
            }

            stale ??= [];
            stale.Add(node);
        }

        if (stale is null) {
            return;
        }

        foreach (var node in stale) {
            _boundaryCaches.Remove(node);
        }
    }
}
