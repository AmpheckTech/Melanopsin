// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Styling;

/// <summary>
/// An immutable lookup structure for one version of a <see cref="StyleSheet"/>.
/// Public stylesheets remain appendable; rebuilding is deferred until their
/// version changes.
/// </summary>
internal sealed class CompiledStyleSheet {
    private static readonly IndexedRule[] EmptyBucket = [];
    private static readonly Comparer<IndexedRule> PrecedenceComparer =
        Comparer<IndexedRule>.Create(static (left, right) => {
            var result = left.Rule.Specificity.CompareTo(right.Rule.Specificity);
            return result != 0 ? result : left.SourceOrder.CompareTo(right.SourceOrder);
        });

    private readonly Dictionary<string, IndexedRule[]> _byType;
    private readonly Dictionary<string, IndexedRule[]> _byId;
    private readonly Dictionary<string, IndexedRule[]> _byClass;
    private readonly IndexedRule[] _universal;
    private readonly IndexedRule[] _layoutStateRules;

    public CompiledStyleSheet(StyleSheet sheet) {
        Version = sheet.Version;

        var byType = new Dictionary<string, List<IndexedRule>>(StringComparer.Ordinal);
        var byId = new Dictionary<string, List<IndexedRule>>(StringComparer.Ordinal);
        var byClass = new Dictionary<string, List<IndexedRule>>(StringComparer.Ordinal);
        var universal = new List<IndexedRule>();
        var layoutStateRules = new List<IndexedRule>();
        for (var index = 0; index < sheet.Rules.Count; index++) {
            var rule = sheet.Rules[index];
            var indexed = new IndexedRule(rule, index);
            AddToBucket(rule.Selector, indexed, byType, byId, byClass, universal);
            if (rule.Selector.HasState &&
                !rule.Selector.HasPseudoElement &&
                rule.Properties.AffectsLayout) {
                layoutStateRules.Add(indexed);
            }
        }

        _byType = Freeze(byType);
        _byId = Freeze(byId);
        _byClass = Freeze(byClass);
        _universal = SortAndFreeze(universal);
        _layoutStateRules = SortAndFreeze(layoutStateRules);
    }

    public long Version { get; }

    public void Apply(Node node, StyleDeclarationApplier declarations, ResolvedStyle style) {
        var matches = FindMatches(node, pseudoElement: null, pseudoElementStates: null);
        Apply(matches, declarations, style);
    }

    public void ApplyPseudoElement(
        Node owner,
        string pseudoElement,
        IReadOnlySet<string> states,
        StyleDeclarationApplier declarations,
        ResolvedStyle style) {
        var matches = FindMatches(owner, pseudoElement, states);
        Apply(matches, declarations, style);
    }

    public bool StateChangeRequiresLayout(Node source) {
        foreach (var indexedRule in _layoutStateRules) {
            if (MatchesSubtreeIgnoringStates(indexedRule.Rule.Selector, source)) {
                return true;
            }
        }

        return false;
    }

    private List<IndexedRule> FindMatches(
        Node node,
        string? pseudoElement,
        IReadOnlySet<string>? pseudoElementStates) {
        var buckets = new List<IndexedRule[]>(capacity: 4);
        AddBucket(buckets, _universal);
        if (node is Element element) {
            AddBucket(buckets, GetBucket(_byType, element.Type));
            if (element.Attributes.TryGetValue("id", out var id) && id is string idText) {
                AddBucket(buckets, GetBucket(_byId, idText));
            }

            if (element.Attributes.TryGetValue("class", out var classes) &&
                classes is string classText) {
                AddClassBuckets(buckets, classText);
            }
        }

        var matches = new List<IndexedRule>();
        foreach (var bucket in buckets) {
            foreach (var indexedRule in bucket) {
                var isMatch = pseudoElement is null
                    ? StyleSelectorMatcher.Matches(indexedRule.Rule.Selector, node)
                    : StyleSelectorMatcher.MatchesPseudoElement(
                        indexedRule.Rule.Selector,
                        node,
                        pseudoElement,
                        pseudoElementStates!);
                if (isMatch) {
                    matches.Add(indexedRule);
                }
            }
        }

        matches.Sort(PrecedenceComparer);
        return matches;
    }

    private static void Apply(
        List<IndexedRule> matches,
        StyleDeclarationApplier declarations,
        ResolvedStyle style) {
        foreach (var match in matches) {
            declarations.Apply(style, match.Rule.Properties);
        }
    }

    private void AddClassBuckets(List<IndexedRule[]> buckets, string classes) {
        var lookup = _byClass.GetAlternateLookup<ReadOnlySpan<char>>();
        var start = 0;
        while (start < classes.Length) {
            while (start < classes.Length && char.IsWhiteSpace(classes[start])) {
                start++;
            }

            var end = start;
            while (end < classes.Length && !char.IsWhiteSpace(classes[end])) {
                end++;
            }

            if (end > start &&
                lookup.TryGetValue(classes.AsSpan(start, end - start), out var bucket)) {
                AddBucket(buckets, bucket);
            }

            start = end;
        }
    }

    private static bool MatchesSubtreeIgnoringStates(
        StyleSelector selector,
        Node node) {
        if (StyleSelectorMatcher.MatchesIgnoringStates(selector, node)) {
            return true;
        }

        if (node is not Element element) {
            return false;
        }

        foreach (var child in element.Children) {
            if (MatchesSubtreeIgnoringStates(selector, child)) {
                return true;
            }
        }

        return false;
    }

    private static void AddBucket(List<IndexedRule[]> buckets, IndexedRule[] bucket) {
        if (bucket.Length != 0 &&
            !buckets.Contains(bucket, ReferenceEqualityComparer.Instance)) {
            buckets.Add(bucket);
        }
    }

    private static IndexedRule[] GetBucket(
        Dictionary<string, IndexedRule[]> buckets,
        string key) {
        return buckets.TryGetValue(key, out var bucket) ? bucket : EmptyBucket;
    }

    private static void AddToBucket(
        StyleSelector selector,
        IndexedRule rule,
        Dictionary<string, List<IndexedRule>> byType,
        Dictionary<string, List<IndexedRule>> byId,
        Dictionary<string, List<IndexedRule>> byClass,
        List<IndexedRule> universal) {
        if (selector.Id is { } id) {
            Add(byId, id, rule);
        } else if (selector.Classes.Count != 0) {
            Add(byClass, selector.Classes[0], rule);
        } else if (selector.TypeName is { } typeName) {
            Add(byType, typeName, rule);
        } else {
            universal.Add(rule);
        }
    }

    private static void Add(
        Dictionary<string, List<IndexedRule>> buckets,
        string key,
        IndexedRule rule) {
        if (!buckets.TryGetValue(key, out var bucket)) {
            bucket = [];
            buckets.Add(key, bucket);
        }

        bucket.Add(rule);
    }

    private static Dictionary<string, IndexedRule[]> Freeze(
        Dictionary<string, List<IndexedRule>> buckets) {
        var frozen = new Dictionary<string, IndexedRule[]>(buckets.Count, StringComparer.Ordinal);
        foreach (var pair in buckets) {
            frozen.Add(pair.Key, SortAndFreeze(pair.Value));
        }

        return frozen;
    }

    private static IndexedRule[] SortAndFreeze(List<IndexedRule> rules) {
        var array = rules.ToArray();
        Array.Sort(array, PrecedenceComparer);
        return array;
    }

    private readonly record struct IndexedRule(StyleRule Rule, int SourceOrder);
}
