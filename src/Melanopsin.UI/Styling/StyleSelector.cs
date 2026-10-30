using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

internal static class StyleSelector {
    public static bool StateChangeRequiresLayout(StyleSheet sheet, Node node) {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(node);
        foreach (var rule in sheet.Rules) {
            var colon = rule.Selector.IndexOf(':');
            if (colon < 0) {
                continue;
            }

            var basic = rule.Selector[..colon];
            if (MatchesBasic(basic, node) &&
                rule.Declarations.Keys.Any(IsLayoutDeclaration)) {
                return true;
            }
        }

        return false;
    }

    public static bool Matches(string selector, Node node) {
        var colon = selector.IndexOf(':');
        var state = colon < 0 ? null : selector[(colon + 1)..];
        var basic = colon < 0 ? selector : selector[..colon];
        if (!MatchesState(state, node)) {
            return false;
        }

        return MatchesBasic(basic, node);
    }

    private static bool MatchesBasic(string basic, Node node) {
        if (node is not Element element) {
            return basic == "Text";
        }

        var hash = basic.IndexOf('#');
        if (hash >= 0) {
            var id = basic[(hash + 1)..];
            return element.Attributes.TryGetValue("id", out var actualId) &&
                   string.Equals(actualId?.ToString(), id, StringComparison.Ordinal);
        }

        var dot = basic.IndexOf('.');
        var type = dot < 0 ? basic : basic[..dot];
        var className = dot < 0 ? null : basic[(dot + 1)..];
        if (type.Length != 0 && type != "*" &&
            !string.Equals(type, element.Type, StringComparison.Ordinal)) {
            return false;
        }

        return className is null || HasClass(element, className);
    }

    private static bool IsLayoutDeclaration(string name) {
        return name is
            "padding" or "margin" or
            "width" or "height" or
            "min-width" or "max-width" or
            "min-height" or "max-height" or
            "flex-grow" or "flex-shrink" or "weight" or "order" or
            "grid-row" or "grid-column" or
            "grid-row-span" or "grid-column-span" or
            "align-self" or "anchor" or "font-size";
    }

    private static bool MatchesState(string? state, Node node) {
        return state is null || node.HasState(state);
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

            if (end > start &&
                text.AsSpan(start, end - start).SequenceEqual(expected)) {
                return true;
            }

            start = end;
        }

        return false;
    }

}
