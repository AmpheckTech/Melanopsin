using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed partial class FocusManager {
    private readonly IWindowHitTester _windows;
    private readonly Dictionary<WindowId, Node> _focused = [];

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
        var hadFocus = _focused.TryGetValue(windowId, out _);
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

}
