using System;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Styling;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class McssLanguageService {
    public static McssHoverInfo? GetHover(
        McssAnalysis analysis,
        string source,
        int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        foreach (var import in analysis.Imports) {
            if (position >= import.Offset && position <= import.Offset + import.Length) {
                var target = import.TargetPath ?? "not found in the current project";
                return new McssHoverInfo(
                    $"MCSS import\n\n`{import.Path}`\n\nResolves to `{target}`.",
                    import.Offset,
                    import.Length);
            }
        }

        if (IsInsideCommentOrString(source, position)) {
            return null;
        }

        if (WordAt(source, position) is not { } word) {
            return null;
        }

        var text = source.Substring(word.Start, word.Length);
        var definition = analysis.Tokens.FirstOrDefault(token =>
            token.NameOffset == word.Start && token.NameLength == word.Length);
        if (definition is not null) {
            return new McssHoverInfo(
                $"MCSS token `${definition.Name}`\n\nType: `{definition.TypeName}`\n\nValue: `{definition.Value}`",
                word.Start,
                word.Length);
        }

        if (word.Start > 0 && source[word.Start - 1] == '$') {
            var token = FindToken(analysis, text);
            return token is null
                ? new McssHoverInfo(
                    $"Unknown MCSS token `${text}`.",
                    word.Start - 1,
                    word.Length + 1)
                : new McssHoverInfo(
                    $"MCSS token `${token.Name}`\n\nType: `{token.TypeName}`\n\nValue: `{token.Value}`",
                    word.Start - 1,
                    word.Length + 1);
        }

        if (word.Start > 0 && source[word.Start - 1] == '@') {
            var directive = "@" + text;
            var description = text.ToLowerInvariant() switch {
                "import" => "Imports another MCSS stylesheet from a relative path.",
                "tokens" => "Declares typed design tokens for this stylesheet.",
                _ => "Unknown MCSS directive.",
            };
            return new McssHoverInfo(
                $"MCSS directive `{directive}`\n\n{description}",
                word.Start - 1,
                word.Length + 1);
        }

        var block = FindInnermostBlock(source, position);
        var statement = StatementAt(source, block, position);
        if (block.Kind == SourceBlockKind.Tokens &&
            statement.Colon >= 0 && statement.Equal > statement.Colon &&
            position > statement.Colon && position <= statement.Equal) {
            var typeName = source.Substring(
                statement.Colon + 1,
                statement.Equal - statement.Colon - 1).Trim();
            return new McssHoverInfo(
                $"MCSS token type `{typeName}`.",
                statement.Colon + 1 + CountLeadingWhitespace(
                    source,
                    statement.Colon + 1,
                    statement.Equal),
                typeName.Length);
        }

        if (block.Kind == SourceBlockKind.Style &&
            statement.Colon >= 0 && !statement.IsBlockHeader &&
            position <= statement.Colon) {
            var property = source.Substring(
                statement.Start,
                statement.Colon - statement.Start).Trim();
            if (CssValueSyntax.TryGetProperty(property, out var valueDefinition)) {
                return new McssHoverInfo(
                    $"MCSS property `{property}`\n\nValue type: `{CssValueSyntax.TokenTypeName(valueDefinition.Kind)}`\n\nMaps to `{valueDefinition.Member}`.",
                    statement.Start,
                    property.Length);
            }
        }

        return null;
    }

    private static int CountLeadingWhitespace(string source, int start, int end) {
        var index = start;
        while (index < end && char.IsWhiteSpace(source[index])) {
            index++;
        }

        return index - start;
    }
}
