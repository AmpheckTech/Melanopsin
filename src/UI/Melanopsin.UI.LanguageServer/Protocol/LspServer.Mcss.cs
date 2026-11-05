// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;
using Melanopsin.UI.LanguageServer.LanguageFeatures;
using Melanopsin.UI.Markup.Compiler.Language;

namespace Melanopsin.UI.LanguageServer.Protocol;

/// <summary>MCSS requests share the document protocol with markup but do not load Roslyn.</summary>
internal sealed partial class LspServer {
    private sealed record McssDocumentState(
        OpenDocument Document,
        IReadOnlyList<McssSourceFile> Sources,
        McssAnalysis Analysis);

    private async Task<McssDocumentState> GetMcssDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var uri = TextDocumentUri(parameters);
        if (!_documents.TryGet(uri, out var document)) {
            throw new InvalidOperationException($"Document '{uri}' is not open.");
        }

        var sources = await _workspace.GetStylesheetsAsync(document.Path, cancellationToken);
        if (!sources.Any(source => string.Equals(source.Path, document.Path, StringComparison.Ordinal))) {
            sources = sources.Append(new McssSourceFile(document.Path, document.Text)).ToArray();
        }

        var analysis = McssLanguageService.Analyze(document.Path, document.Text, sources);
        return new McssDocumentState(document, sources, analysis);
    }

    private async Task<JsonArray> McssCompletionAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, state.Document);
        var items = McssLanguageService.GetCompletions(
            state.Analysis,
            state.Document.Text,
            offset);
        var (replaceStart, replaceEnd) = McssWordBounds(state.Document.Text, offset);
        var range = PositionMapper.Range(
            state.Document.Text,
            replaceStart,
            replaceEnd - replaceStart);
        var result = new JsonArray();
        foreach (var item in items) {
            var completion = new JsonObject {
                ["label"] = item.Label,
                ["kind"] = McssCompletionItemKind(item.Kind),
                ["detail"] = item.Detail,
                ["textEdit"] = new JsonObject {
                    ["range"] = JsonSerializer.SerializeToNode(range),
                    ["newText"] = item.InsertText,
                },
                ["insertTextFormat"] = item.InsertText.Contains("$0", StringComparison.Ordinal) ? 2 : 1,
            };
            if (item.Documentation is not null) {
                completion["documentation"] = new JsonObject {
                    ["kind"] = "markdown",
                    ["value"] = item.Documentation,
                };
            }

            result.Add(completion);
        }

        return result;
    }

    private async Task<JsonObject?> McssHoverAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, state.Document);
        var hover = McssLanguageService.GetHover(state.Analysis, state.Document.Text, offset);
        return hover is null
            ? null
            : new JsonObject {
                ["contents"] = new JsonObject {
                    ["kind"] = "markdown",
                    ["value"] = hover.Markdown,
                },
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    state.Document.Text,
                    hover.Offset,
                    hover.Length)),
            };
    }

    private async Task<JsonArray> McssDefinitionAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, state.Document);
        if (McssLanguageService.FindDefinition(
                state.Analysis,
                state.Document.Text,
                offset) is not { } definition) {
            return new JsonArray();
        }

        var text = state.Sources.FirstOrDefault(source =>
            string.Equals(source.Path, definition.Path, StringComparison.Ordinal))?.Text;
        if (text is null && File.Exists(definition.Path)) {
            text = await File.ReadAllTextAsync(definition.Path, cancellationToken);
        }

        return [
            Location(
                PathToUri(definition.Path),
                text ?? string.Empty,
                definition.Offset,
                definition.Length),
        ];
    }

    private async Task<JsonArray> McssReferencesAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, state.Document);
        var name = McssTokenNameAt(state.Document.Text, offset);
        if (name is null) {
            return new JsonArray();
        }

        var result = new JsonArray();
        foreach (var span in McssLanguageService.FindTokenSpans(
                     state.Analysis,
                     state.Document.Text,
                     name)) {
            result.Add(Location(
                PathToUri(span.Path),
                state.Document.Text,
                span.Offset,
                span.Length));
        }

        return result;
    }

    private async Task<JsonArray> McssDocumentHighlightAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, state.Document);
        var name = McssTokenNameAt(state.Document.Text, offset);
        if (name is null) {
            return new JsonArray();
        }

        var result = new JsonArray();
        foreach (var span in McssLanguageService.FindTokenSpans(
                     state.Analysis,
                     state.Document.Text,
                     name)) {
            AddHighlight(result, state.Document.Text, span.Offset, span.Length);
        }

        return result;
    }

    private async Task<JsonArray> McssDocumentSymbolsAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var result = new JsonArray();
        foreach (var token in state.Analysis.Tokens.OrderBy(static item => item.NameOffset)) {
            var range = PositionMapper.Range(
                state.Document.Text,
                token.NameOffset,
                token.NameLength);
            result.Add(new JsonObject {
                ["name"] = "$" + token.Name,
                ["detail"] = token.TypeName + " = " + token.Value,
                ["kind"] = 14,
                ["range"] = JsonSerializer.SerializeToNode(range),
                ["selectionRange"] = JsonSerializer.SerializeToNode(range),
            });
        }

        foreach (var rule in state.Analysis.Rules) {
            var range = PositionMapper.Range(
                state.Document.Text,
                rule.Offset,
                rule.Length);
            result.Add(new JsonObject {
                ["name"] = rule.Selector,
                ["detail"] = "MCSS rule",
                ["kind"] = 5,
                ["range"] = JsonSerializer.SerializeToNode(range),
                ["selectionRange"] = JsonSerializer.SerializeToNode(range),
            });
        }

        return result;
    }

    private async Task<JsonObject> McssSemanticTokensAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var state = await GetMcssDocumentAsync(parameters, cancellationToken);
        var tokens = McssLanguageService.GetSemanticTokens(
            state.Analysis,
            state.Document.Text);
        var data = new JsonArray();
        var previousLine = 0;
        var previousCharacter = 0;
        foreach (var token in tokens.OrderBy(static item => item.Offset)) {
            var (line, character) = PositionMapper.ToPosition(state.Document.Text, token.Offset);
            data.Add(line - previousLine);
            data.Add(line == previousLine ? character - previousCharacter : character);
            data.Add(token.Length);
            data.Add(McssTokenTypeIndex(token.Kind));
            data.Add(0);
            previousLine = line;
            previousCharacter = character;
        }

        return new JsonObject { ["data"] = data };
    }

    private JsonArray McssSelectionRanges(JsonElement parameters) {
        var uri = TextDocumentUri(parameters);
        if (!_documents.TryGet(uri, out var document)) {
            throw new InvalidOperationException($"Document '{uri}' is not open.");
        }

        var result = new JsonArray();
        foreach (var position in parameters.GetProperty("positions").EnumerateArray()) {
            var offset = PositionMapper.ToOffset(
                document.Text,
                position.GetProperty("line").GetInt32(),
                position.GetProperty("character").GetInt32());
            var (start, end) = McssWordBounds(document.Text, offset);
            var parent = new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    0,
                    document.Text.Length)),
            };
            var current = new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    start,
                    end - start)),
                ["parent"] = parent,
            };
            result.Add(current);
        }

        return result;
    }

    private static string? McssTokenNameAt(string text, int offset) {
        var (start, end) = McssWordBounds(text, offset);
        if (start == end) {
            return null;
        }

        var name = text.Substring(start, end - start).TrimStart('$');
        return name.Length == 0 ? null : name;
    }

    private static (int Start, int End) McssWordBounds(string text, int offset) {
        var start = Math.Min(Math.Max(offset, 0), text.Length);
        var end = start;
        while (start > 0 && IsMcssWordCharacter(text[start - 1])) {
            start--;
        }

        while (end < text.Length && IsMcssWordCharacter(text[end])) {
            end++;
        }

        return (start, end);
    }

    private static bool IsMcssWordCharacter(char value) {
        return char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or '$' or '@';
    }

    private static int McssCompletionItemKind(McssCompletionKind kind) {
        return kind switch {
            McssCompletionKind.Directive => 14,
            McssCompletionKind.Property => 10,
            McssCompletionKind.Token => 6,
            McssCompletionKind.Value => 12,
            McssCompletionKind.Selector => 7,
            McssCompletionKind.Snippet => 15,
            _ => 1,
        };
    }

    private static int McssTokenTypeIndex(McssSemanticTokenKind kind) {
        return kind switch {
            McssSemanticTokenKind.Directive => 5,
            McssSemanticTokenKind.Type => 0,
            McssSemanticTokenKind.Class => 1,
            McssSemanticTokenKind.Property => 3,
            McssSemanticTokenKind.State => 8,
            McssSemanticTokenKind.Variable => 6,
            McssSemanticTokenKind.Value => 10,
            McssSemanticTokenKind.Number => 9,
            McssSemanticTokenKind.Comment => 11,
            _ => 0,
        };
    }
}
