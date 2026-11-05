// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed partial class FocusManager {
    private readonly Dictionary<WindowId, List<ModalFocusCheckpoint>> _modalStacks = [];

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
            if (node is OverlayHost { HasDismissibleOverlay: true } host &&
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

    private static bool IsActiveModal(Overlay overlay) {
        return overlay.IsOpen && overlay.IsMounted;
    }
}
