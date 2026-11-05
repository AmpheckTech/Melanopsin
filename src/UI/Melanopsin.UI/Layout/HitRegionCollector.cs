// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

internal sealed class HitRegionCollector {
    private readonly Dictionary<Node, HitRegion> _regionsByNode =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<HitRegion> _regions = [];

    public int CacheHits { get; private set; }

    public int CacheMisses { get; private set; }

    public IReadOnlyList<HitRegion> Collect(LayoutNode root) {
        _regions.Clear();
        CacheHits = 0;
        CacheMisses = 0;
        Collect(root, _regions);
        return _regions;
    }

    public void Prune(LayoutSnapshot snapshot) {
        if (_regionsByNode.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in _regionsByNode.Keys) {
            if (snapshot.Contains(node)) {
                continue;
            }

            stale ??= [];
            stale.Add(node);
        }

        if (stale is null) {
            return;
        }

        foreach (var node in stale) {
            _regionsByNode.Remove(node);
        }
    }

    private void Collect(
        LayoutNode layoutNode,
        List<HitRegion> hitRegions) {
        if (layoutNode.WasArrangementReused &&
            layoutNode.CachedHitRegions is { } cachedHitRegions) {
            hitRegions.AddRange(cachedHitRegions);
            CacheHits += cachedHitRegions.Length;
            return;
        }

        var startIndex = hitRegions.Count;
        if (IsHitTestVisible(layoutNode.Node)) {
            if (layoutNode.CachedHitRegion is { } cachedHitRegion) {
                hitRegions.Add(cachedHitRegion);
                CacheHits++;
            } else {
                var route = BuildRoute(layoutNode);
                if (!_regionsByNode.TryGetValue(
                        layoutNode.Node,
                        out var hitRegion)) {
                    hitRegion = new HitRegion(
                        layoutNode.Node,
                        layoutNode.Bounds,
                        route,
                        layoutNode.ClipBounds);
                    _regionsByNode[layoutNode.Node] = hitRegion;
                } else {
                    hitRegion.Update(
                        layoutNode.Bounds,
                        route,
                        layoutNode.ClipBounds);
                }

                hitRegions.Add(hitRegion);
                layoutNode.CachedHitRegion = hitRegion;
                CacheMisses++;
            }
        }

        foreach (var child in layoutNode.Children) {
            Collect(child, hitRegions);
        }

        if (layoutNode.Node is IAdditionalHitRegionProvider additionalRegions) {
            var route = BuildRoute(layoutNode);
            var bounds = new List<Rect>(2);
            additionalRegions.AppendHitRegionBounds(bounds);
            foreach (var hitBounds in bounds) {
                hitRegions.Add(new HitRegion(
                    layoutNode.Node,
                    hitBounds,
                    route,
                    layoutNode.ClipBounds));
            }
        }

        if (layoutNode.Node.IsRelayoutBoundary) {
            layoutNode.CachedHitRegions = hitRegions
                .GetRange(startIndex, hitRegions.Count - startIndex)
                .ToArray();
        }
    }

    private static List<Node> BuildRoute(LayoutNode layoutNode) {
        var route = new List<Node>();
        for (var current = layoutNode;
             current is not null;
             current = current.Parent) {
            route.Add(current.Node);
        }

        route.Reverse();
        return route;
    }

    private static bool IsHitTestVisible(Node node) {
        if (node is Control control) {
            return control.IsHitTestVisible;
        }

        if (node is Element element &&
            element.Attributes.TryGetValue("input", out var value)) {
            return !string.Equals(
                value?.ToString(),
                "none",
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
