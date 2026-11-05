// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using Melanopsin.UI.LanguageServer.LanguageFeatures;
using Melanopsin.UI.Markup.Compiler.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.LanguageServer.Protocol;

internal sealed partial class LspServer {
    private static JsonObject? ClosingTagHover(OpenDocument document, int offset) {
        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        if (pair is null || !IsClosingTagPosition(pair, offset)) {
            return null;
        }

        return new JsonObject {
            ["contents"] = new JsonObject {
                ["kind"] = "markdown",
                ["value"] = $"**</{pair.Name}>**\n\nClosing tag for `<{pair.Name}>`.",
            },
            ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                document.Text,
                pair.ClosingNameStart!.Value,
                pair.ClosingNameLength!.Value)),
        };
    }

    private static JsonObject? TagDefinition(OpenDocument document, int offset) {
        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        if (pair is null || !IsClosingTagPosition(pair, offset)) {
            return null;
        }

        return Location(
            document.Uri,
            document.Text,
            pair.OpeningNameStart,
            pair.OpeningNameLength);
    }

    private async Task<JsonArray> ReferencesAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        if (TryCodeSymbol(compilation, document, offset, cancellationToken, out var symbol, out _)) {
            return CodeLocations(
                document,
                CodeSpansOf(compilation, document, symbol, cancellationToken));
        }

        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        return pair is null ? [] : TagLocations(document, pair);
    }

    private async Task<JsonArray> DocumentHighlightAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        if (TryCodeSymbol(compilation, document, offset, cancellationToken, out var symbol, out _)) {
            return CodeHighlights(
                document,
                CodeSpansOf(compilation, document, symbol, cancellationToken));
        }

        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        if (pair is null) {
            return [];
        }

        var result = new JsonArray();
        AddHighlight(result, document.Text, pair.OpeningNameStart, pair.OpeningNameLength);
        if (pair.ClosingNameStart is { } closingStart &&
            pair.ClosingNameLength is { } closingLength) {
            AddHighlight(result, document.Text, closingStart, closingLength);
        }

        return result;
    }

    private async Task<JsonObject?> PrepareRenameAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        if (TryCodeSymbol(compilation, document, offset, cancellationToken, out var symbol, out var span)) {
            return new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    span.Offset,
                    span.Length)),
                ["placeholder"] = symbol.Name,
            };
        }

        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        if (pair is null) {
            return null;
        }

        var closing = IsClosingTagPosition(pair, offset);
        return new JsonObject {
            ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                document.Text,
                closing ? pair.ClosingNameStart!.Value : pair.OpeningNameStart,
                closing ? pair.ClosingNameLength!.Value : pair.OpeningNameLength)),
            ["placeholder"] = pair.Name,
        };
    }

    private async Task<JsonObject?> RenameAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        var newName = parameters.GetProperty("newName").GetString() ?? string.Empty;
        if (TryCodeSymbol(compilation, document, offset, cancellationToken, out var symbol, out _)) {
            // IsValidIdentifier accepts reserved words, which would not compile as a member name.
            if (!SyntaxFacts.IsValidIdentifier(newName) ||
                SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None) {
                throw new InvalidOperationException($"'{newName}' is not a valid C# identifier.");
            }

            var codeEdits = new JsonArray();
            foreach (var span in CodeSpansOf(compilation, document, symbol, cancellationToken)) {
                codeEdits.Add(TextEdit(document.Text, span.Offset, span.Length, newName));
            }

            return new JsonObject {
                ["changes"] = new JsonObject {
                    [document.Uri] = codeEdits,
                },
            };
        }

        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        if (pair is null) {
            return null;
        }

        if (!IsValidTagName(newName)) {
            throw new InvalidOperationException($"'{newName}' is not a valid markup tag name.");
        }

        var edits = new JsonArray {
            TextEdit(
                document.Text,
                pair.OpeningNameStart,
                pair.OpeningNameLength,
                newName),
        };
        if (pair.ClosingNameStart is { } closingStart &&
            pair.ClosingNameLength is { } closingLength) {
            edits.Add(TextEdit(document.Text, closingStart, closingLength, newName));
        }

        return new JsonObject {
            ["changes"] = new JsonObject {
                [document.Uri] = edits,
            },
        };
    }

    private async Task<JsonArray> CodeActionsAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, _) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = RangeStartOffset(parameters, document);
        var result = new JsonArray();
        var pair = MarkupLanguageService.GetTagPair(document.Text, offset);
        foreach (var action in MarkupLanguageService.GetTagCodeActions(document.Text, offset)) {
            var edits = new JsonArray();
            foreach (var edit in action.Edits.OrderByDescending(static edit => edit.Offset)) {
                edits.Add(TextEdit(document.Text, edit.Offset, edit.Length, edit.NewText));
            }

            result.Add(new JsonObject {
                ["title"] = action.Title,
                ["kind"] = action.Kind,
                ["isPreferred"] = action.IsPreferred,
                ["edit"] = new JsonObject {
                    ["changes"] = new JsonObject {
                        [document.Uri] = edits,
                    },
                },
            });
        }

        if (pair is { IsPaired: true }) {
            var target = IsClosingTagPosition(pair, offset)
                ? pair.OpeningNameStart
                : pair.ClosingNameStart!.Value;
            var (line, character) = PositionMapper.ToPosition(document.Text, target);
            result.Add(new JsonObject {
                ["title"] = "Go to Matching Tag",
                ["kind"] = "refactor",
                ["command"] = new JsonObject {
                    ["title"] = "Go to Matching Tag",
                    ["command"] = "melanopsin.mxml.goToMatchingTag",
                    ["arguments"] = new JsonArray(
                        document.Uri,
                        new JsonObject {
                            ["line"] = line,
                            ["character"] = character,
                        }),
                },
            });
        }

        return result;
    }

    private async Task<JsonArray> SelectionRangeAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var result = new JsonArray();
        foreach (var position in parameters.GetProperty("positions").EnumerateArray()) {
            var offset = PositionMapper.ToOffset(
                document.Text,
                position.GetProperty("line").GetInt32(),
                position.GetProperty("character").GetInt32());
            var spans = MarkupLanguageService.GetElementSpans(document.Text, offset);
            // Spans go from the innermost element outwards; a code identifier starts the chain.
            var chain = new List<(int Offset, int Length)>();
            if (TryCodeSymbol(
                    compilation,
                    document,
                    offset,
                    cancellationToken,
                    out _,
                    out var codeSpan)) {
                chain.Add((codeSpan.Offset, codeSpan.Length));
            }

            chain.AddRange(spans.Select(static span => (span.Offset, span.Length)));
            JsonObject? current = null;
            for (var index = chain.Count - 1; index >= 0; index--) {
                var span = chain[index];
                current = new JsonObject {
                    ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                        document.Text,
                        span.Offset,
                        span.Length)),
                    ["parent"] = current,
                };
            }

            result.Add(current ?? new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    offset,
                    0)),
                ["parent"] = null,
            });
        }

        return result;
    }

    private static JsonArray TagLocations(OpenDocument document, MarkupTagPair pair) {
        var result = new JsonArray {
            Location(
                document.Uri,
                document.Text,
                pair.OpeningNameStart,
                pair.OpeningNameLength),
        };
        if (pair.ClosingNameStart is { } closingStart &&
            pair.ClosingNameLength is { } closingLength) {
            result.Add(Location(document.Uri, document.Text, closingStart, closingLength));
        }

        return result;
    }

    private static JsonObject Location(
        string uri,
        string text,
        int offset,
        int length) {
        return new JsonObject {
            ["uri"] = uri,
            ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                text,
                offset,
                length)),
        };
    }

    private static JsonObject TextEdit(
        string text,
        int offset,
        int length,
        string newText) {
        return new JsonObject {
            ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                text,
                offset,
                length)),
            ["newText"] = newText,
        };
    }

    private static void AddHighlight(
        JsonArray target,
        string text,
        int offset,
        int length) {
        target.Add(new JsonObject {
            ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                text,
                offset,
                length)),
            ["kind"] = 2,
        });
    }

    private static int RangeStartOffset(JsonElement parameters, OpenDocument document) {
        var start = parameters.GetProperty("range").GetProperty("start");
        return PositionMapper.ToOffset(
            document.Text,
            start.GetProperty("line").GetInt32(),
            start.GetProperty("character").GetInt32());
    }

    private static bool IsClosingTagPosition(MarkupTagPair pair, int offset) {
        return pair.ClosingTagStart is { } tagStart &&
               pair.ClosingTagEnd is { } tagEnd &&
               offset >= tagStart &&
               offset <= tagEnd;
    }

    private static bool IsValidTagName(string name) {
        if (string.IsNullOrEmpty(name)) {
            return false;
        }

        var separator = name.IndexOf(':');
        if (separator < 0) {
            return IsValidXmlName(name);
        }

        return separator > 0 &&
               separator < name.Length - 1 &&
               name.IndexOf(':', separator + 1) < 0 &&
               IsValidXmlName(name.Substring(0, separator)) &&
               IsValidXmlName(name.Substring(separator + 1));
    }

    private static bool IsValidXmlName(string name) {
        try {
            XmlConvert.VerifyNCName(name);
            return true;
        } catch (XmlException) {
            return false;
        }
    }
}
