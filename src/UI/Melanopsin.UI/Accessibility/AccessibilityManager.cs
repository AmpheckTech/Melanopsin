using Melanopsin.UI.App;

namespace Melanopsin.UI.Accessibility;

public sealed class AccessibilityManager {
    private readonly IWindowTreeQuery _windows;
    private readonly IAccessibilityBridge? _bridge;

    public AccessibilityManager(
        IWindowTreeQuery windows,
        IAccessibilityBridge? bridge = null) {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = windows;
        _bridge = bridge;
    }

    public void Publish(WindowId windowId) {
        var root = _windows.GetRoot(windowId);
        if (root is null) {
            return;
        }

        _bridge?.Publish(BuildTree(windowId, root));
    }

    public void PublishAll() {
        foreach (var windowId in _windows.WindowIds) {
            var root = _windows.GetRoot(windowId);
            if (root is not null) {
                _bridge?.Publish(BuildTree(windowId, root));
            }
        }
    }

    private AccessibilityTree BuildTree(WindowId windowId, Node root) {
        return AccessibilityTreeBuilder.Build(
            windowId,
            root,
            node => _windows.TryGetBounds(windowId, node, out var bounds)
                ? bounds
                : null);
    }
}
