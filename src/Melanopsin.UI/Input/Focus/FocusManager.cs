using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed class FocusManager {
    private readonly IWindowHitTester _windows;
    private readonly Dictionary<WindowId, Node> _focused = [];
    private readonly Dictionary<WindowId, List<ModalFocusCheckpoint>> _modalStacks = [];

    public FocusManager(IWindowHitTester windows) {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = windows;
    }

    public Node? GetFocused(WindowId windowId) {
        var node = GetMountedFocus(windowId);
        if (node is null) {
            return null;
        }

        var modal = FindTopModalOverlay(windowId);
        return modal is not null && !IsWithin(node, modal)
            ? null
            : node;
    }

    private Node? GetMountedFocus(WindowId windowId) {
        if (!_focused.TryGetValue(windowId, out var node)) {
            return null;
        }

        if (node.IsMounted && CanFocus(node)) {
            return node;
        }

        node.IsFocused = false;
        _focused.Remove(windowId);
        return null;
    }

    public bool Focus(WindowId windowId, Node? node) {
        if (node is not null && !CanFocus(node)) {
            return false;
        }

        var modal = FindTopModalOverlay(windowId);
        if (modal is not null &&
            node is not null &&
            !IsWithin(node, modal)) {
            return false;
        }

        EnsureModalCheckpoint(windowId, modal);
        return SetFocus(windowId, node);
    }

    public bool FocusNext(WindowId windowId, bool reverse = false) {
        var focusable = GetFocusable(windowId);
        if (focusable.Count == 0) {
            return SetFocus(windowId, null);
        }

        var current = GetFocused(windowId);
        var currentIndex = current is null ? -1 : focusable.IndexOf(current);
        var nextIndex = reverse
            ? (currentIndex <= 0 ? focusable.Count - 1 : currentIndex - 1)
            : (currentIndex + 1) % focusable.Count;
        return Focus(windowId, focusable[nextIndex]);
    }

    public bool HandlePointer(PointerInputEvent input) {
        if (input.Phase != PointerPhase.Down) {
            return false;
        }

        var target = _windows.HitTest(input.WindowId, input.X, input.Y);
        if (target is null) {
            return Focus(input.WindowId, null);
        }

        for (var index = target.Route.Count - 1; index >= 0; index--) {
            if (target.Route[index] is { IsFocusable: true } focusTarget &&
                CanFocus(focusTarget)) {
                var effectiveTarget = focusTarget is IFocusProxy proxy
                    ? proxy.FocusTarget
                    : focusTarget;
                return Focus(input.WindowId, effectiveTarget);
            }
        }

        return Focus(input.WindowId, null);
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down) {
            return false;
        }

        if (input.Key == Key.Tab) {
            return FocusNext(input.WindowId, input.Shift);
        }

        if (input.Key == Key.Escape &&
            DismissTopOverlay(input.WindowId) is not null) {
            return true;
        }

        var focused = GetFocused(input.WindowId);
        if (focused is IKeyboardInputHandler keyboardInput &&
            keyboardInput.HandleKey(input)) {
            return true;
        }

        if (input.Key is Key.Enter or Key.Space &&
            focused is IKeyboardActivationTarget activationTarget) {
            activationTarget.Activate();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Repairs focus after modal or keyed tree changes.
    /// </summary>
    public bool Reconcile(WindowId windowId) {
        if (!_windows.ContainsWindow(windowId)) {
            CancelWindow(windowId);
            return false;
        }

        var modal = FindTopModalOverlay(windowId);
        var hadFocus = _focused.ContainsKey(windowId);
        var focused = GetFocused(windowId);
        Node? restore = null;
        if (_modalStacks.TryGetValue(windowId, out var stack)) {
            while (stack.Count != 0 &&
                   !IsActiveModal(stack[^1].Modal)) {
                restore = stack[^1].PreviousFocus ?? restore;
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count == 0) {
                _modalStacks.Remove(windowId);
            }
        }

        if (modal is not null) {
            if (focused is not null && IsWithin(focused, modal)) {
                return false;
            }

            EnsureModalCheckpoint(windowId, modal);
            var first = GetFocusable(windowId).FirstOrDefault();
            return SetFocus(windowId, first);
        }

        if (restore is not null && restore.IsMounted) {
            return SetFocus(windowId, restore);
        }

        if (focused is not null) {
            return false;
        }

        if (!hadFocus) {
            return false;
        }

        var fallback = GetFocusable(windowId).FirstOrDefault();
        return fallback is not null && SetFocus(windowId, fallback);
    }

    public void CancelWindow(WindowId windowId) {
        SetFocus(windowId, null);
        _modalStacks.Remove(windowId);
    }

    private bool SetFocus(WindowId windowId, Node? node) {
        if (ReferenceEquals(GetFocused(windowId), node)) {
            return false;
        }

        if (_focused.TryGetValue(windowId, out var previous)) {
            previous.IsFocused = false;
        }

        if (node is null) {
            _focused.Remove(windowId);
            return true;
        }

        _focused[windowId] = node;
        node.IsFocused = true;
        return true;
    }

    private void EnsureModalCheckpoint(
        WindowId windowId,
        Overlay? modal) {
        if (modal is null) {
            return;
        }

        if (!_modalStacks.TryGetValue(windowId, out var stack)) {
            stack = [];
            _modalStacks[windowId] = stack;
        }

        if (stack.Count != 0 &&
            ReferenceEquals(stack[^1].Modal, modal)) {
            return;
        }

        var previous = GetMountedFocus(windowId);
        stack.Add(new ModalFocusCheckpoint(
            modal,
            previous is not null && previous.IsMounted
                ? previous
                : null));
    }

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

    private Overlay? FindTopModalOverlay(WindowId windowId) {
        var root = _windows.GetRoot(windowId);
        if (root is null) {
            return null;
        }

        Overlay? result = null;
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count != 0) {
            var node = pending.Pop();
            if (node is OverlayHost host) {
                var modal = host.Overlays.LastOrDefault(
                    static overlay => overlay.IsModal && overlay.IsOpen);
                if (modal is not null) {
                    result = modal;
                }
            }

            var children = GetChildren(node);
            for (var index = children.Count - 1; index >= 0; index--) {
                pending.Push(children[index]);
            }
        }

        return result;
    }

    private Overlay? DismissTopOverlay(WindowId windowId) {
        var root = _windows.GetRoot(windowId);
        if (root is null) {
            return null;
        }

        OverlayHost? targetHost = null;
        Overlay? targetOverlay = null;
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count != 0) {
            var node = pending.Pop();
            if (node is OverlayHost { HasModalOverlay: true } host &&
                host.TopOverlay is { } overlay) {
                targetHost = host;
                targetOverlay = overlay;
            }

            var children = GetChildren(node);
            for (var index = children.Count - 1; index >= 0; index--) {
                pending.Push(children[index]);
            }
        }

        if (targetHost is null || targetOverlay is null) {
            return null;
        }

        _ = targetHost.Close(targetOverlay, "escape");
        return targetOverlay;
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

    private static bool IsActiveModal(Overlay overlay) {
        return overlay.IsOpen && overlay.IsMounted;
    }

}
