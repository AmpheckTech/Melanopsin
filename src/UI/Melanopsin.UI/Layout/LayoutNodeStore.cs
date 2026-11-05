// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Layout;

internal sealed class LayoutNodeStore {
    private readonly Dictionary<Node, LayoutNode> _nodes =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Node, LayoutConstraints> _measureConstraints =
        new(ReferenceEqualityComparer.Instance);
    private bool _allowArrangeReuse;

    public int Count => _nodes.Count;

    public int ArrangeReuseHits { get; private set; }

    public int ArrangeReuseMisses { get; private set; }

    public void BeginPass(bool allowArrangeReuse) {
        _measureConstraints.Clear();
        _allowArrangeReuse = allowArrangeReuse;
        ArrangeReuseHits = 0;
        ArrangeReuseMisses = 0;
    }

    public void RememberMeasureConstraints(
        Node node,
        LayoutConstraints constraints) {
        _measureConstraints[node] = constraints;
    }

    public LayoutNode GetOrCreate(
        Node node,
        Rect bounds,
        LayoutNode? parent,
        Rect? inheritedClip,
        long layoutRevision,
        long styleSheetVersion,
        UiMetrics metrics,
        bool usesViewportUnits) {
        var measureConstraints = _measureConstraints.TryGetValue(
            node,
            out var currentConstraints)
            ? currentConstraints
            : (LayoutConstraints?)null;

        if (!_nodes.TryGetValue(node, out var layoutNode)) {
            ArrangeReuseMisses++;
            layoutNode = new LayoutNode(
                node,
                bounds,
                parent,
                inheritedClip,
                measureConstraints,
                layoutRevision,
                styleSheetVersion,
                metrics,
                usesViewportUnits);
            _nodes[node] = layoutNode;
            return layoutNode;
        }

        if (_allowArrangeReuse &&
            layoutNode.TryReuseArrangement(
                bounds,
                parent,
                inheritedClip,
                measureConstraints,
                layoutRevision,
                styleSheetVersion,
                metrics,
                usesViewportUnits)) {
            ArrangeReuseHits++;
            return layoutNode;
        }

        ArrangeReuseMisses++;
        layoutNode.Update(
            bounds,
            parent,
            inheritedClip,
            measureConstraints,
            layoutRevision,
            styleSheetVersion,
            metrics,
            usesViewportUnits);
        return layoutNode;
    }

    public void Prune(LayoutSnapshot snapshot) {
        if (_nodes.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in _nodes.Keys) {
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
            _nodes.Remove(node);
        }
    }
}
