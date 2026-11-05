// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

/// <summary>
/// Builds an accessibility tree from the live node hierarchy. Resolvers return
/// <see langword="null"/> for transparent containers that do not own layout boxes;
/// those containers are omitted while their laid-out descendants remain visible.
/// </summary>
public static class AccessibilityTreeBuilder {
    public static AccessibilityTree Build(
        WindowId windowId,
        Node root,
        Func<Node, Rect?> resolveBounds) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(resolveBounds);
        var nodes = new Dictionary<AccessibilityNodeId, Node>();
        var rootBounds = resolveBounds(root) ??
            throw new InvalidOperationException(
                $"Accessibility root '{root.GetType().Name}' is not present in the layout.");
        var accessibilityRoot = CreateNode(root, rootBounds, nodes);
        AddChildren(root, accessibilityRoot.Children, resolveBounds, nodes);
        return new AccessibilityTree(windowId, accessibilityRoot, nodes);
    }

    private static void AddChildren(
        Node node,
        List<AccessibilityNode> destination,
        Func<Node, Rect?> resolveBounds,
        Dictionary<AccessibilityNodeId, Node> nodes) {
        foreach (var child in GetChildren(node)) {
            if (!child.Accessibility.IsHidden) {
                AddNode(child, destination, resolveBounds, nodes);
            }
        }
    }

    private static void AddNode(
        Node node,
        List<AccessibilityNode> destination,
        Func<Node, Rect?> resolveBounds,
        Dictionary<AccessibilityNodeId, Node> nodes) {
        if (resolveBounds(node) is not { } bounds) {
            AddChildren(node, destination, resolveBounds, nodes);
            return;
        }

        var result = CreateNode(node, bounds, nodes);
        destination.Add(result);
        AddChildren(node, result.Children, resolveBounds, nodes);
    }

    private static AccessibilityNode CreateNode(
        Node node,
        Rect bounds,
        Dictionary<AccessibilityNodeId, Node> nodes) {
        var properties = node.Accessibility;
        var id = node.AccessibilityId;
        var actions = node is IAccessibilityActionProvider provider
            ? provider.SupportedActions
            : AccessibilityAction.None;
        var result = new AccessibilityNode(
            id,
            properties.Name,
            properties.Role,
            bounds,
            node.IsFocused,
            properties.Description,
            properties.Value,
            ResolveState(node),
            actions);
        nodes.Add(id, node);
        return result;
    }

    private static AccessibilityState ResolveState(Node node) {
        var state = AccessibilityState.None;
        state = Set(node, UiStateNames.Disabled, state, AccessibilityState.Disabled);
        state = Set(node, UiStateNames.Pressed, state, AccessibilityState.Pressed);
        state = Set(node, UiStateNames.Checked, state, AccessibilityState.Checked);
        state = Set(node, UiStateNames.Selected, state, AccessibilityState.Selected);
        state = Set(node, UiStateNames.Expanded, state, AccessibilityState.Expanded);
        state = Set(node, UiStateNames.ReadOnly, state, AccessibilityState.ReadOnly);
        state = Set(node, UiStateNames.Invalid, state, AccessibilityState.Invalid);
        return Set(node, UiStateNames.Busy, state, AccessibilityState.Busy);
    }

    private static AccessibilityState Set(
        Node node,
        string name,
        AccessibilityState state,
        AccessibilityState flag) {
        return node.HasState(name) ? state | flag : state;
    }

    private static IReadOnlyList<Node> GetChildren(Node node) {
        return node is Element element ? element.Children : [];
    }
}
