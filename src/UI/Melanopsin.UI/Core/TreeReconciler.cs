// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

internal static class TreeReconciler {
    public static Node Reconcile(Node current, Node incoming) {
        if (!CanReuse(current, incoming)) {
            return incoming;
        }

        if (current is Element currentElement &&
            incoming is Element incomingElement) {
            var invalidation = ElementMerger.ReplaceAttributes(
                currentElement,
                incomingElement,
                out var layoutReason);
            var reconciledInvalidation =
                currentElement.OnReconciledFrom(incomingElement);
            invalidation |= reconciledInvalidation;
            if ((reconciledInvalidation & InvalidationKind.Layout) != 0) {
                layoutReason = LayoutInvalidationReason.Unknown;
            }

            ElementMerger.ReplaceText(currentElement, incomingElement);
            ElementMerger.ReplaceBindings(currentElement, incomingElement);
            if (currentElement.ReconcilesChildren) {
                ReconcileChildren(currentElement, incomingElement);
            }

            currentElement.Invalidate(invalidation, layoutReason, affectsStyle: true);
            return currentElement;
        }

        if (current is TextNode currentText &&
            incoming is TextNode incomingText &&
            !string.Equals(currentText.Text, incomingText.Text, StringComparison.Ordinal)) {
            return incomingText;
        }

        return current;
    }

    private static bool CanReuse(Node current, Node incoming) {
        if (current.GetType() != incoming.GetType()) {
            return false;
        }

        if (current.ReconciliationSlot >= 0 &&
            incoming.ReconciliationSlot >= 0 &&
            current.ReconciliationSlot != incoming.ReconciliationSlot) {
            return false;
        }

        return current.ReconciliationKey is null && incoming.ReconciliationKey is null ||
               Equals(current.ReconciliationKey, incoming.ReconciliationKey);
    }

    private static void ReconcileChildren(Element current, Element incoming) {
        var currentChildren = current.Children.ToList();
        var incomingChildren = incoming.Children.ToList();
        var keyed = new Dictionary<object, Node>();
        var unkeyed = new Dictionary<(Type Type, int Slot), Queue<Node>>();
        foreach (var child in currentChildren) {
            if (child.ReconciliationKey is { } key) {
                if (!keyed.TryAdd(key, child)) {
                    throw new InvalidOperationException(
                        $"Duplicate reconciliation key '{key}' among siblings.");
                }

                continue;
            }

            var identity = (child.GetType(), child.ReconciliationSlot);
            if (!unkeyed.TryGetValue(identity, out var queue)) {
                queue = new Queue<Node>();
                unkeyed[identity] = queue;
            }

            queue.Enqueue(child);
        }

        var usedKeys = new HashSet<object>();
        var reconciled = new List<Node>(incomingChildren.Count);
        foreach (var incomingChild in incomingChildren) {
            Node? currentChild = null;
            if (incomingChild.ReconciliationKey is { } key) {
                if (!usedKeys.Add(key)) {
                    throw new InvalidOperationException(
                        $"Duplicate reconciliation key '{key}' among incoming siblings.");
                }

                if (keyed.Remove(key, out var keyedChild) &&
                    keyedChild.GetType() == incomingChild.GetType() &&
                    SlotsMatch(keyedChild, incomingChild)) {
                    currentChild = keyedChild;
                }
            } else {
                var identity = (
                    incomingChild.GetType(),
                    incomingChild.ReconciliationSlot);
                if (unkeyed.TryGetValue(identity, out var queue) &&
                    queue.Count != 0) {
                    currentChild = queue.Dequeue();
                }
            }

            if (currentChild is null) {
                incoming.DetachChildForTransfer(incomingChild);
                reconciled.Add(incomingChild);
                continue;
            }

            var merged = Reconcile(currentChild, incomingChild);
            if (!ReferenceEquals(merged, currentChild)) {
                incoming.DetachChildForTransfer(merged);
            }

            reconciled.Add(merged);
        }

        current.ReplaceChildren(reconciled);
    }

    private static bool SlotsMatch(Node left, Node right) {
        return left.ReconciliationSlot < 0 ||
               right.ReconciliationSlot < 0 ||
               left.ReconciliationSlot == right.ReconciliationSlot;
    }
}
