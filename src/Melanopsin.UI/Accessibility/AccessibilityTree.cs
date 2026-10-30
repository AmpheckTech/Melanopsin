using Melanopsin.UI.Threading;

namespace Melanopsin.UI.Accessibility;

public sealed class AccessibilityTree {
    private readonly IReadOnlyDictionary<AccessibilityNodeId, Node> _nodes;

    internal AccessibilityTree(
        WindowId windowId,
        AccessibilityNode root,
        IReadOnlyDictionary<AccessibilityNodeId, Node> nodes) {
        WindowId = windowId;
        Root = root;
        _nodes = nodes;
    }

    public WindowId WindowId { get; }

    public AccessibilityNode Root { get; }

    public bool TryPerformAction(
        AccessibilityNodeId nodeId,
        AccessibilityAction action) {
        var dispatcher = UiDispatcher.Default;
        if (dispatcher is not null && !dispatcher.CheckAccess()) {
            return false;
        }

        if (!IsSingleAction(action) ||
            !_nodes.TryGetValue(nodeId, out var node) ||
            node is not IAccessibilityActionProvider provider ||
            (provider.SupportedActions & action) != action) {
            return false;
        }

        return provider.PerformAccessibilityAction(action);
    }

    private static bool IsSingleAction(AccessibilityAction action) {
        var value = (int)action;
        return value > 0 && (value & (value - 1)) == 0;
    }
}
