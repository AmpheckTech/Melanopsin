// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Layout;

public sealed partial class FlowLayoutEngine {
    private readonly ConditionalWeakTable<Element, VisibleChildrenCache>
        _visibleChildren = new();
    private readonly Dictionary<Node, bool> _viewportDependencies =
        new(ReferenceEqualityComparer.Instance);
    private HashSet<Node>? _invalidationPath;

    private IReadOnlyList<Node> VisibleChildren(Element element) {
        if (_visibleChildren.TryGetValue(element, out var cached) &&
            cached.StructureRevision == element.SubtreeStructureRevision) {
            return cached.Children;
        }

        var children = new List<Node>(element.Children.Count);
        AddVisibleChildren(element, children);
        var visible = new VisibleChildrenCache(
            element.SubtreeStructureRevision,
            children.ToArray());
        _visibleChildren.Remove(element);
        _visibleChildren.Add(element, visible);
        return visible.Children;
    }

    private bool UsesViewportUnits(Node node) {
        if (_viewportDependencies.TryGetValue(node, out var usesViewportUnits)) {
            return usesViewportUnits;
        }

        if (node is ILayoutViewportDependency dependency) {
            usesViewportUnits = dependency.UsesViewportUnits;
        } else if (node is ILayoutParticipant or ILayoutIntrinsicProvider) {
            // Custom layout participants may read the viewport directly. They
            // opt into viewport-independent caching by implementing the
            // dependency contract.
            usesViewportUnits = true;
        }

        if (node is Element element) {
            usesViewportUnits |= _styleEngine.ResolveReadOnly(node).UsesViewportUnits;
            if (!usesViewportUnits) {
                foreach (var child in element.Children) {
                    if (!UsesViewportUnits(child)) {
                        continue;
                    }

                    usesViewportUnits = true;
                    break;
                }
            }
        }

        _viewportDependencies[node] = usesViewportUnits;
        return usesViewportUnits;
    }

    private static HashSet<Node>? BuildInvalidationPath(
        Node root,
        IReadOnlyCollection<Node> invalidatedLayoutNodes) {
        var path = new HashSet<Node>(ReferenceEqualityComparer.Instance) {
            root,
        };
        foreach (var invalidatedNode in invalidatedLayoutNodes) {
            Node? current = invalidatedNode;
            while (current is not null) {
                path.Add(current);
                if (ReferenceEquals(current, root)) {
                    break;
                }

                current = current.Parent;
            }

            if (current is null) {
                return null;
            }
        }

        return path;
    }

    private static void AddVisibleChildren(
        Element parent,
        List<Node> children) {
        foreach (var child in parent.Children) {
            if (!IsNodeVisible(child)) {
                continue;
            }

            if (child is Fragment fragment) {
                AddVisibleChildren(fragment, children);
            } else {
                children.Add(child);
            }
        }
    }

    private static bool IsNodeVisible(Node node) {
        return node is not Control { IsVisible: false };
    }

    private sealed class VisibleChildrenCache(
        long structureRevision,
        IReadOnlyList<Node> children) {
        public long StructureRevision { get; } = structureRevision;

        public IReadOnlyList<Node> Children { get; } = children;
    }
}
