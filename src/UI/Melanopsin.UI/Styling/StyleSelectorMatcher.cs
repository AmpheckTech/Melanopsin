// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Styling;

internal static class StyleSelectorMatcher {
    public static bool Matches(StyleSelector selector, Node node) {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(node);
        return MatchesSelector(selector, node, ignoreStates: false);
    }

    public static bool MatchesIgnoringStates(StyleSelector selector, Node node) {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(node);
        return MatchesSelector(selector, node, ignoreStates: true);
    }

    public static bool MatchesPseudoElement(
        StyleSelector selector,
        Node owner,
        string pseudoElement,
        IReadOnlySet<string> pseudoElementStates) {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(pseudoElement);
        ArgumentNullException.ThrowIfNull(pseudoElementStates);
        if (!string.Equals(
                selector.PseudoElement,
                pseudoElement,
                StringComparison.Ordinal) ||
            !MatchesCompound(
                selector,
                owner,
                pseudoElementStates,
                ignoreStates: false)) {
            return false;
        }

        return MatchesParent(selector, owner, ignoreStates: false);
    }

    private static bool MatchesSelector(
        StyleSelector selector,
        Node node,
        bool ignoreStates) {
        if (!MatchesCompound(selector, node, pseudoElementStates: null, ignoreStates)) {
            return false;
        }

        return MatchesParent(selector, node, ignoreStates);
    }

    private static bool MatchesParent(
        StyleSelector selector,
        Node node,
        bool ignoreStates) {
        if (selector.Parent is null) {
            return true;
        }

        if (selector.Combinator == StyleCombinator.Child) {
            return node.Parent is { } parent &&
                MatchesSelector(selector.Parent, parent, ignoreStates);
        }

        for (var parent = node.Parent; parent is not null; parent = parent.Parent) {
            if (MatchesSelector(selector.Parent, parent, ignoreStates)) {
                return true;
            }
        }

        return false;
    }

    internal static bool MatchesCompound(
        StyleSelector selector,
        Node node,
        IReadOnlySet<string>? pseudoElementStates,
        bool ignoreStates) {
        if (selector.PseudoElement is null) {
            if (pseudoElementStates is not null) {
                return false;
            }
        } else {
            if (pseudoElementStates is null) {
                return false;
            }

            if (!ignoreStates) {
                foreach (var state in selector.PseudoElementStates) {
                    if (!pseudoElementStates.Contains(state)) {
                        return false;
                    }
                }
            }
        }

        if (!ignoreStates) {
            foreach (var state in selector.States) {
                if (!node.HasState(state)) {
                    return false;
                }
            }
        }

        if (selector.TypeName is null &&
            selector.Id is null &&
            selector.Classes.Count == 0) {
            return true;
        }

        if (node is not Element element) {
            return false;
        }

        if (selector.TypeName is not null &&
            !string.Equals(selector.TypeName, element.Type, StringComparison.Ordinal)) {
            return false;
        }

        if (selector.Id is not null &&
            (!element.Attributes.TryGetValue("id", out var id) ||
             !string.Equals(id?.ToString(), selector.Id, StringComparison.Ordinal))) {
            return false;
        }

        foreach (var className in selector.Classes) {
            if (!HasClass(element, className)) {
                return false;
            }
        }

        return true;
    }

    private static bool HasClass(Element element, string expected) {
        if (!element.Attributes.TryGetValue("class", out var value) ||
            value is not string text) {
            return false;
        }

        var start = 0;
        while (start < text.Length) {
            while (start < text.Length && char.IsWhiteSpace(text[start])) {
                start++;
            }

            var end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) {
                end++;
            }

            if (end > start && text.AsSpan(start, end - start).SequenceEqual(expected)) {
                return true;
            }

            start = end;
        }

        return false;
    }
}
