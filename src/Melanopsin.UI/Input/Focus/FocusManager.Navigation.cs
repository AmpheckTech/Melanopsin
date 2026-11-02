using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed partial class FocusManager {
    private List<Node> GetFocusable(WindowId windowId) {
        var focusable = new List<Node>();
        var root = _windows.GetRoot(windowId);
        if (root is null) {
            return focusable;
        }

        var modal = FindTopModalOverlay(windowId);
        if (modal is null) {
            CollectFocusable(root, focusable);
        } else {
            foreach (var child in GetChildren(modal)) {
                CollectFocusable(child, focusable);
            }

            if (focusable.Count == 0 && CanFocus(modal)) {
                focusable.Add(modal);
            }
        }

        return focusable;
    }

    private static void CollectFocusable(
        Node node,
        List<Node> focusable) {
        if (node.IsFocusable &&
            CanFocus(node) &&
            node is not OverlayBackdrop &&
            node is not IFocusProxy) {
            focusable.Add(node);
        }

        foreach (var child in GetChildren(node)) {
            CollectFocusable(child, focusable);
        }
    }

    private static IReadOnlyList<Node> GetChildren(Node node) {
        return node is Element element ? element.Children : [];
    }

    private static bool CanFocus(Node node) {
        if (!node.IsMounted || !node.IsFocusable) {
            return false;
        }

        return node is not Control control ||
               control.IsVisible && control.IsEnabled;
    }

    private static bool IsWithin(Node node, Node container) {
        for (var current = node; current is not null; current = current.Parent) {
            if (ReferenceEquals(current, container)) {
                return true;
            }
        }

        return false;
    }
}
