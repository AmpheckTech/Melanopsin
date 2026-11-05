using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Styling;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>Editor analysis backed by the same parser and validators used at compile time.</summary>
public static partial class McssLanguageService {
    private static readonly string[] StateNames = [
        "hover", "pressed", "focused", "disabled", "selected",
    ];

    public static McssAnalysis Analyze(
        string path,
        string source,
        IReadOnlyList<McssSourceFile>? relatedSources = null) {
        var sources = MergeSources(path, source, relatedSources);
        var sheets = sources.Select(file => CssParser.Parse(file.Path, file.Text)).ToArray();
        var catalog = CssStylesheetCatalog.Create(sheets);
        var entry = catalog.Entries.First(item => CssStylesheetPath.Comparer.Equals(
            CssStylesheetPath.Normalize(item.Stylesheet.Path),
            CssStylesheetPath.Normalize(path)));
        var emission = CssEmitter.Emit(entry.Stylesheet, string.Empty, entry.Imports);
        var diagnostics = emission.Diagnostics
            .Concat(entry.Diagnostics)
            .GroupBy(static diagnostic => (
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Offset,
                diagnostic.Length))
            .Select(static group => group.First())
            .Select(static diagnostic => new McssDiagnosticInfo(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Offset,
                Math.Max(1, diagnostic.Length)))
            .ToArray();

        var sheetsByPath = CssStylesheetPath.CreateIndex(sheets);
        var tokens = entry.Stylesheet.Slots
            .Select(slot => ToTokenInfo(source, slot))
            .ToArray();
        var imports = entry.Stylesheet.Imports
            .Select(import => new McssImportInfo(
                import.Path,
                import.Offset,
                import.Length,
                ResolveImportPath(entry.Stylesheet.Path, import.Path, sheetsByPath)))
            .ToArray();
        var rules = entry.Stylesheet.Rules
            .GroupBy(static rule => rule.Offset)
            .Select(group => ToRuleInfo(source, group.Key))
            .Where(static rule => rule is not null)
            .Cast<McssRuleInfo>()
            .OrderBy(static rule => rule.Offset)
            .ToArray();

        return new McssAnalysis(path, diagnostics, tokens, imports, rules);
    }

    private static IReadOnlyList<McssSourceFile> MergeSources(
        string path,
        string source,
        IReadOnlyList<McssSourceFile>? relatedSources) {
        var normalizedPath = CssStylesheetPath.Normalize(path);
        var sources = new List<McssSourceFile> { new(path, source) };
        if (relatedSources is null) {
            return sources;
        }

        foreach (var related in relatedSources) {
            if (!related.Path.EndsWith(".mcss", StringComparison.OrdinalIgnoreCase) ||
                CssStylesheetPath.Comparer.Equals(
                    CssStylesheetPath.Normalize(related.Path),
                    normalizedPath)) {
                continue;
            }

            sources.Add(related);
        }

        return sources;
    }

    private static McssTokenInfo ToTokenInfo(string source, CssSlot slot) {
        var nameOffset = TokenNameOffset(source, slot.Offset);
        return new McssTokenInfo(
            slot.Name,
            slot.TypeName,
            slot.Value,
            nameOffset,
            slot.Name.Length,
            slot.Offset,
            slot.Value.Length);
    }

    private static int TokenNameOffset(string source, int valueOffset) {
        var colon = source.LastIndexOf(':', Math.Max(0, valueOffset - 1));
        if (colon < 0) {
            return valueOffset;
        }

        var end = colon - 1;
        while (end >= 0 && char.IsWhiteSpace(source[end])) {
            end--;
        }

        var start = end;
        while (start > 0 && IsIdentifierCharacter(source[start - 1])) {
            start--;
        }

        return Math.Max(0, start);
    }

    private static McssRuleInfo? ToRuleInfo(string source, int offset) {
        if (offset < 0 || offset >= source.Length) {
            return null;
        }

        var brace = source.IndexOf('{', offset);
        if (brace < 0) {
            return null;
        }

        var end = brace;
        while (end > offset && char.IsWhiteSpace(source[end - 1])) {
            end--;
        }

        var selector = source.Substring(offset, end - offset).Trim();
        return selector.Length == 0
            ? null
            : new McssRuleInfo(selector, offset, selector.Length);
    }

    private static string? ResolveImportPath(
        string sourcePath,
        string importPath,
        IReadOnlyDictionary<string, CssStylesheet> sheetsByPath) {
        if (Path.IsPathRooted(importPath)) {
            return null;
        }

        try {
            var candidate = CssStylesheetPath.Normalize(Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? string.Empty,
                importPath.Replace('/', Path.DirectorySeparatorChar)));
            return sheetsByPath.TryGetValue(candidate, out var sheet) ? sheet.Path : null;
        } catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return null;
        }
    }

    private static IReadOnlyList<McssCompletionItem> TokenCompletions(McssAnalysis analysis) {
        return analysis.Tokens.Select(token => new McssCompletionItem(
            "$" + token.Name,
            token.TypeName,
            "$" + token.Name,
            McssCompletionKind.Token,
            $"`{token.TypeName}` = `{token.Value}`")).ToArray();
    }

    private static McssTokenInfo? FindToken(McssAnalysis analysis, string name) {
        return analysis.Tokens.FirstOrDefault(token =>
            string.Equals(token.Name, name, StringComparison.Ordinal));
    }
}
