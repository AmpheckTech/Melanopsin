using System;
using System.Collections.Generic;
using System.Linq;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class McssLanguageService {
    public static IReadOnlyList<McssCompletionItem> GetCompletions(
        McssAnalysis analysis,
        string source,
        int offset) {
        if (IsInsideCommentOrString(source, offset)) {
            return Array.Empty<McssCompletionItem>();
        }

        var position = Math.Min(Math.Max(offset, 0), source.Length);
        var wordStart = CompletionWordStart(source, position);
        var prefix = source.Substring(wordStart, position - wordStart);
        if (prefix.StartsWith("@", StringComparison.Ordinal)) {
            return DirectiveCompletions();
        }

        var block = FindInnermostBlock(source, position);
        var statement = StatementAt(source, block, position);
        if (block.Kind == SourceBlockKind.Tokens) {
            return TokenBlockCompletions(analysis, source, statement, position, prefix);
        }

        if (block.Kind == SourceBlockKind.TopLevel || statement.IsBlockHeader) {
            return SelectorCompletions(analysis, source, statement, position, prefix);
        }

        if (statement.Colon < 0 || position <= statement.Colon) {
            return McssSyntaxCatalog.PropertyCompletions();
        }

        var property = source.AsSpan(
            statement.Start,
            statement.Colon - statement.Start).Trim().ToString();
        var items = new List<McssCompletionItem>();
        if (prefix.StartsWith("$", StringComparison.Ordinal)) {
            items.AddRange(TokenCompletions(analysis));
        } else {
            items.AddRange(TokenCompletions(analysis));
            items.AddRange(McssSyntaxCatalog.ValueCompletions(property));
        }

        return Distinct(items);
    }

    private static IReadOnlyList<McssCompletionItem> TokenBlockCompletions(
        McssAnalysis analysis,
        string source,
        SourceStatement statement,
        int offset,
        string prefix) {
        if (statement.Colon < 0 || offset <= statement.Colon) {
            return Array.Empty<McssCompletionItem>();
        }

        if (statement.Equal < 0 || offset <= statement.Equal) {
            return McssSyntaxCatalog.TokenTypeCompletions();
        }

        var typeName = source.Substring(
            statement.Colon + 1,
            statement.Equal - statement.Colon - 1).Trim();
        var items = new List<McssCompletionItem>();
        if (prefix.StartsWith("$", StringComparison.Ordinal)) {
            items.AddRange(TokenCompletions(analysis));
        } else {
            items.AddRange(TokenCompletions(analysis));
            items.AddRange(McssSyntaxCatalog.TokenValueCompletions(typeName));
        }

        return Distinct(items);
    }

    private static IReadOnlyList<McssCompletionItem> SelectorCompletions(
        McssAnalysis analysis,
        string source,
        SourceStatement statement,
        int offset,
        string prefix) {
        if (statement.Colon >= 0 && offset > statement.Colon) {
            return StateNames.Select(state => new McssCompletionItem(
                state,
                "MCSS state",
                state,
                McssCompletionKind.Value)).ToArray();
        }

        var typeNames = analysis.Rules
            .Select(rule => FirstSelectorType(rule.Selector))
            .Where(static name => name is not null)
            .Select(static name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal);
        return typeNames.Select(name => new McssCompletionItem(
            name,
            "MCSS selector",
            name,
            McssCompletionKind.Selector)).ToArray();
    }

    private static IReadOnlyList<McssCompletionItem> DirectiveCompletions() {
        return [
            new(
                "@import",
                "MCSS directive",
                "@import \"$0\";",
                McssCompletionKind.Snippet,
                "Imports another MCSS stylesheet using a path relative to this file."),
            new(
                "@tokens",
                "MCSS directive",
                "@tokens {\n    $0\n}",
                McssCompletionKind.Snippet,
                "Declares typed MCSS design tokens."),
        ];
    }

    private static IReadOnlyList<McssCompletionItem> Distinct(
        IEnumerable<McssCompletionItem> items) {
        return items
            .GroupBy(
                static item => (item.Label, item.Kind, item.InsertText),
                static item => item)
            .Select(static group => group.First())
            .ToArray();
    }

    private static string? FirstSelectorType(string selector) {
        var index = 0;
        while (index < selector.Length && char.IsWhiteSpace(selector[index])) {
            index++;
        }

        if (index >= selector.Length ||
            selector[index] is '.' or '#' or ':' or '*' or '&' or '>' or '+') {
            return null;
        }

        var start = index++;
        while (index < selector.Length && IsIdentifierCharacter(selector[index])) {
            index++;
        }

        return index > start ? selector.Substring(start, index - start) : null;
    }

    private static int CompletionWordStart(string source, int offset) {
        var index = offset;
        while (index > 0 && IsWordCharacter(source[index - 1])) {
            index--;
        }

        return index;
    }

    private static bool IsInsideCommentOrString(string source, int offset) {
        var index = 0;
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        while (index < position) {
            if (StartsComment(source, index)) {
                var end = SkipComment(source, index);
                if (position <= end) {
                    return true;
                }

                index = end;
                continue;
            }

            if (source[index] is '\'' or '"') {
                var end = SkipString(source, index);
                if (position <= end) {
                    return true;
                }

                index = end;
                continue;
            }

            index++;
        }

        return false;
    }
}
