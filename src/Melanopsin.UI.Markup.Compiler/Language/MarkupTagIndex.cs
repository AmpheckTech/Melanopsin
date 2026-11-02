using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// A tolerant index of raw MXML tags. It intentionally does not depend on a
/// successful XML parse so editor features keep working while a view is broken.
/// </summary>
internal sealed class MarkupTagIndex {
    private readonly List<MarkupTagToken> _tokens;

    private MarkupTagIndex(
        List<MarkupTagToken> tokens,
        IReadOnlyList<MarkupTagIssue> issues) {
        _tokens = tokens;
        Issues = issues;
    }

    public IReadOnlyList<MarkupTagIssue> Issues { get; }

    public static MarkupTagIndex Parse(string source) {
        var tokens = MarkupTagScanner.Scan(source);
        var issues = new List<MarkupTagIssue>();
        MatchTags(tokens, issues);
        return new MarkupTagIndex(tokens, issues);
    }

    /// <summary>Nests the scanned tags and reports the pairs that do not line up.</summary>
    private static void MatchTags(
        List<MarkupTagToken> tokens,
        List<MarkupTagIssue> issues) {
        var open = new List<int>();
        for (var index = 0; index < tokens.Count; index++) {
            var token = tokens[index];
            token.ParentIndex = open.Count == 0 ? -1 : open[open.Count - 1];
            if (token.Kind == MarkupTagTokenKind.Closing) {
                MatchClosingTag(tokens, open, issues, index);
            } else if (!token.IsSelfClosing) {
                open.Add(index);
            }
        }

        foreach (var index in open) {
            issues.Add(ToIssue(MarkupTagIssueKind.UnclosedOpeningTag, tokens[index], null));
        }
    }

    public MarkupTagPair? FindPairAt(string source, int offset) {
        var tokenIndex = FindTokenIndexAt(offset);
        if (tokenIndex < 0) {
            return null;
        }

        var token = _tokens[tokenIndex];
        var openingIndex = token.Kind == MarkupTagTokenKind.Opening
            ? tokenIndex
            : token.MatchIndex;
        if (openingIndex < 0) {
            return null;
        }

        var opening = _tokens[openingIndex];
        return ToPair(source, opening);
    }

    public MarkupTagIssue? FindIssueAt(int offset) {
        MarkupTagIssue? best = null;
        foreach (var issue in Issues) {
            if (offset < issue.TagStart || offset > issue.TagEnd) {
                continue;
            }

            if (best is null ||
                issue.TagEnd - issue.TagStart < best.TagEnd - best.TagStart) {
                best = issue;
            }
        }

        return best;
    }

    public bool TryFindElementRange(
        int offset,
        out int start,
        out int end,
        out int parentIndex) {
        start = 0;
        end = 0;
        parentIndex = -1;
        var bestIndex = -1;
        var bestLength = int.MaxValue;
        for (var index = 0; index < _tokens.Count; index++) {
            var token = _tokens[index];
            if (token.Kind != MarkupTagTokenKind.Opening) {
                continue;
            }

            var elementEnd = token.MatchIndex >= 0
                ? _tokens[token.MatchIndex].TagEnd
                : token.TagEnd;
            if (offset < token.TagStart || offset > elementEnd) {
                continue;
            }

            var length = elementEnd - token.TagStart;
            if (length < bestLength) {
                bestIndex = index;
                bestLength = length;
                start = token.TagStart;
                end = elementEnd;
            }
        }

        if (bestIndex < 0) {
            return false;
        }

        parentIndex = _tokens[bestIndex].ParentIndex;
        return true;
    }

    public bool TryGetElementRange(
        int elementIndex,
        out int start,
        out int end,
        out int parentIndex) {
        if (elementIndex < 0 || elementIndex >= _tokens.Count) {
            start = 0;
            end = 0;
            parentIndex = -1;
            return false;
        }

        var token = _tokens[elementIndex];
        start = token.TagStart;
        end = token.MatchIndex >= 0 ? _tokens[token.MatchIndex].TagEnd : token.TagEnd;
        parentIndex = token.ParentIndex;
        return true;
    }

    public IReadOnlyList<MarkupSourceSpan> GetElementSpans(int offset) {
        if (!TryFindElementRange(offset, out var start, out var end, out var parent)) {
            return Array.Empty<MarkupSourceSpan>();
        }

        var spans = new List<MarkupSourceSpan>();
        AddSpan(start, end);
        while (parent >= 0 && TryGetElementRange(parent, out start, out end, out var next)) {
            AddSpan(start, end);
            parent = next;
        }

        return spans;

        void AddSpan(int spanStart, int spanEnd) {
            spans.Add(new MarkupSourceSpan(spanStart, spanEnd - spanStart));
        }
    }

    private MarkupTagPair ToPair(string source, MarkupTagToken opening) {
        var closing = opening.MatchIndex >= 0 ? _tokens[opening.MatchIndex] : null;
        return new MarkupTagPair(
            opening.Name,
            opening.TagStart,
            opening.TagEnd,
            opening.NameStart,
            opening.NameEnd - opening.NameStart,
            closing?.TagStart,
            closing?.TagEnd,
            closing is null ? null : closing.NameStart,
            closing is null ? null : closing.NameEnd - closing.NameStart,
            opening.IsSelfClosing,
            HasContent(source, opening, closing),
            opening.ParentIndex < 0);
    }

    private int FindTokenIndexAt(int offset) {
        var bestIndex = -1;
        var bestLength = int.MaxValue;
        for (var index = 0; index < _tokens.Count; index++) {
            var token = _tokens[index];
            if (offset < token.TagStart || offset > token.TagEnd) {
                continue;
            }

            var length = token.TagEnd - token.TagStart;
            if (length < bestLength) {
                bestIndex = index;
                bestLength = length;
            }
        }

        return bestIndex;
    }

    private static void MatchClosingTag(
        List<MarkupTagToken> tokens,
        List<int> open,
        List<MarkupTagIssue> issues,
        int closingIndex) {
        var closing = tokens[closingIndex];
        if (open.Count == 0) {
            issues.Add(ToIssue(MarkupTagIssueKind.UnexpectedClosingTag, closing, null));
            return;
        }

        var matchingPosition = -1;
        for (var index = open.Count - 1; index >= 0; index--) {
            if (tokens[open[index]].Name == closing.Name) {
                matchingPosition = index;
                break;
            }
        }

        if (matchingPosition < 0) {
            issues.Add(ToIssue(
                MarkupTagIssueKind.UnexpectedClosingTag,
                closing,
                tokens[open[open.Count - 1]].Name));
            return;
        }

        for (var index = open.Count - 1; index > matchingPosition; index--) {
            issues.Add(ToIssue(
                MarkupTagIssueKind.UnclosedOpeningTag,
                tokens[open[index]],
                null));
        }

        var openingIndex = open[matchingPosition];
        tokens[openingIndex].MatchIndex = closingIndex;
        tokens[closingIndex].MatchIndex = openingIndex;
        open.RemoveRange(matchingPosition, open.Count - matchingPosition);
    }

    private static MarkupTagIssue ToIssue(
        MarkupTagIssueKind kind,
        MarkupTagToken token,
        string? expectedName) {
        return new MarkupTagIssue(
            kind,
            token.TagStart,
            token.TagEnd,
            token.NameStart,
            token.NameEnd - token.NameStart,
            token.Name,
            expectedName);
    }

    private static bool HasContent(
        string source,
        MarkupTagToken opening,
        MarkupTagToken? closing) {
        if (opening.IsSelfClosing || closing is null) {
            return false;
        }

        for (var index = opening.TagEnd; index < closing.TagStart; index++) {
            if (!char.IsWhiteSpace(source[index])) {
                return true;
            }
        }

        return false;
    }

}
