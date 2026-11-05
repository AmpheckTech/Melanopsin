using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class MarkupLanguageService {
    /// <summary>Finds the opening/closing tag pair that owns an editor position.</summary>
    public static MarkupTagPair? GetTagPair(string source, int offset) {
        return MarkupTagIndex.Parse(source).FindPairAt(source, offset);
    }

    /// <summary>Returns element spans from the innermost tag to the document root.</summary>
    public static IReadOnlyList<MarkupSourceSpan> GetElementSpans(
        string source,
        int offset) {
        return MarkupTagIndex.Parse(source).GetElementSpans(offset);
    }

    /// <summary>Returns editor actions that can be applied to the tag at a position.</summary>
    public static IReadOnlyList<MarkupCodeActionInfo> GetTagCodeActions(
        string source,
        int offset) {
        var index = MarkupTagIndex.Parse(source);
        var actions = new List<MarkupCodeActionInfo>();
        var pair = index.FindPairAt(source, offset);
        if (pair is not null) {
            AddPairActions(source, pair, actions);
        }

        var issue = index.FindIssueAt(offset);
        if (issue is not null) {
            AddRecoveryActions(source, pair, issue, actions);
        }

        return actions;
    }

    private static void AddPairActions(
        string source,
        MarkupTagPair pair,
        List<MarkupCodeActionInfo> actions) {
        if (pair.IsSelfClosing) {
            var opening = source.Substring(
                pair.OpeningTagStart,
                pair.OpeningTagEnd - pair.OpeningTagStart);
            if (opening.EndsWith("/>", StringComparison.Ordinal)) {
                var expanded = opening.Substring(0, opening.Length - 2) +
                               "></" + pair.Name + ">";
                actions.Add(new MarkupCodeActionInfo(
                    $"Expand <{pair.Name}> to a paired tag",
                    "refactor.rewrite",
                    [new MarkupTextEdit(
                        pair.OpeningTagStart,
                        pair.OpeningTagEnd - pair.OpeningTagStart,
                        expanded)]));
            }

            return;
        }

        if (!pair.IsPaired || pair.ClosingTagEnd is not { } closingEnd) {
            return;
        }

        if (!pair.HasContent) {
            var opening = source.Substring(
                pair.OpeningTagStart,
                pair.OpeningTagEnd - pair.OpeningTagStart).TrimEnd();
            if (opening.EndsWith(">", StringComparison.Ordinal)) {
                actions.Add(new MarkupCodeActionInfo(
                    $"Collapse <{pair.Name}> to a self-closing tag",
                    "refactor.rewrite",
                    [new MarkupTextEdit(
                        pair.OpeningTagStart,
                        closingEnd - pair.OpeningTagStart,
                        opening.Substring(0, opening.Length - 1) + " />")]));
            }

            return;
        }

        if (!pair.IsRoot) {
            actions.Add(new MarkupCodeActionInfo(
                $"Unwrap <{pair.Name}>",
                "refactor.rewrite",
                [
                    new MarkupTextEdit(
                        pair.OpeningTagStart,
                        pair.OpeningTagEnd - pair.OpeningTagStart,
                        string.Empty),
                    new MarkupTextEdit(
                        pair.ClosingTagStart!.Value,
                        closingEnd - pair.ClosingTagStart.Value,
                        string.Empty),
                ]));
        }
    }

    private static void AddRecoveryActions(
        string source,
        MarkupTagPair? pair,
        MarkupTagIssue issue,
        List<MarkupCodeActionInfo> actions) {
        if (issue.Kind == MarkupTagIssueKind.UnexpectedClosingTag &&
            issue.ExpectedName is { Length: > 0 } expectedName) {
            actions.Add(new MarkupCodeActionInfo(
                $"Replace </{issue.Name}> with </{expectedName}>",
                "quickfix",
                [new MarkupTextEdit(
                    issue.NameStart,
                    issue.NameLength,
                    expectedName)],
                IsPreferred: true));
        }

        if (issue.Kind == MarkupTagIssueKind.UnexpectedClosingTag) {
            actions.Add(new MarkupCodeActionInfo(
                $"Remove unexpected closing tag </{issue.Name}>",
                "quickfix",
                [new MarkupTextEdit(
                    issue.TagStart,
                    issue.TagEnd - issue.TagStart,
                    string.Empty)]));
        }

        if (issue.Kind == MarkupTagIssueKind.UnclosedOpeningTag) {
            actions.Add(new MarkupCodeActionInfo(
                $"Add closing tag for <{issue.Name}>",
                "quickfix",
                [new MarkupTextEdit(
                    issue.TagEnd,
                    0,
                    $"</{issue.Name}>")],
                IsPreferred: true));
            if (pair is { IsSelfClosing: false, HasContent: false } &&
                pair.OpeningTagEnd > pair.OpeningTagStart) {
                var opening = source.Substring(
                    pair.OpeningTagStart,
                    pair.OpeningTagEnd - pair.OpeningTagStart).TrimEnd();
                actions.Add(new MarkupCodeActionInfo(
                    $"Make <{issue.Name}> self-closing",
                    "quickfix",
                    [new MarkupTextEdit(
                        pair.OpeningTagStart,
                        pair.OpeningTagEnd - pair.OpeningTagStart,
                        opening.EndsWith(">", StringComparison.Ordinal)
                            ? opening.Substring(0, opening.Length - 1) + " />"
                            : opening + " />")]));
            }
        }
    }
}
