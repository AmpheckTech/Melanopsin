using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Melanopsin.UI.LanguageServer.LanguageFeatures;
using Melanopsin.UI.LanguageServer.Workspace;
using Melanopsin.UI.Markup.Compiler.Language;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.LanguageServer.Protocol;

internal sealed partial class LspServer : IDisposable {
    private static readonly JsonElement EmptyParameters =
        JsonSerializer.SerializeToElement(new JsonObject());
    private static readonly string[] TokenTypes =
    [
        "type", "class", "className", "property", "event", "keyword", "variable",
        "function", "enumMember", "number", "string", "comment",
    ];
    private readonly JsonRpcConnection _connection;
    private readonly DocumentStore _documents = new();
    private readonly MarkupWorkspace _workspace;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _requests = new();
    private readonly HashSet<Task> _requestTasks = [];
    private readonly object _requestTasksLock = new();
    private readonly object _preloadLock = new();
    private readonly object _diagnosticsLock = new();
    private CancellationTokenSource? _preloadCancellation;
    private CancellationTokenSource? _diagnosticsCancellation;

    public LspServer(JsonRpcConnection connection) {
        _connection = connection;
        _workspace = new MarkupWorkspace(() => _documents
            .Snapshot()
            .Select(static document => new MarkupBuffer(document.Path, document.Text))
            .ToArray());
    }

    public async Task RunAsync(CancellationToken cancellationToken = default) {
        while (true) {
            var message = await _connection.ReadAsync(cancellationToken);
            if (message is null) {
                return;
            }

            var root = message.Value;
            if (!root.TryGetProperty("method", out var methodElement)) {
                continue;
            }

            var method = methodElement.GetString() ?? string.Empty;
            if (method == "exit") {
                return;
            }

            var parameters = root.TryGetProperty("params", out var value)
                ? value
                : EmptyParameters;
            if (root.TryGetProperty("id", out var id)) {
                if (method is "initialize" or "shutdown") {
                    await HandleRequestAsync(
                        id,
                        method,
                        parameters,
                        cancellationToken,
                        cancellation: null);
                } else {
                    StartRequest(id, method, parameters, cancellationToken);
                }

                continue;
            }

            HandleNotification(method, parameters);
        }
    }

    public void Dispose() {
        foreach (var request in _requests.Values) {
            request.Cancel();
        }

        lock (_diagnosticsLock) {
            _diagnosticsCancellation?.Cancel();
            _diagnosticsCancellation = null;
        }

        lock (_preloadLock) {
            _preloadCancellation?.Cancel();
            _preloadCancellation = null;
        }

        _workspace.Dispose();
        _connection.Dispose();
    }

    private async Task HandleRequestAsync(
        JsonElement id,
        string method,
        JsonElement parameters,
        CancellationToken cancellationToken,
        CancellationTokenSource? cancellation) {
        try {
            var result = method switch {
                "initialize" => Initialize(parameters),
                "shutdown" => Shutdown(),
                "textDocument/completion" when IsMcssDocument(parameters) =>
                    await McssCompletionAsync(parameters, cancellationToken),
                "textDocument/hover" when IsMcssDocument(parameters) =>
                    await McssHoverAsync(parameters, cancellationToken),
                "textDocument/definition" when IsMcssDocument(parameters) =>
                    await McssDefinitionAsync(parameters, cancellationToken),
                "textDocument/references" when IsMcssDocument(parameters) =>
                    await McssReferencesAsync(parameters, cancellationToken),
                "textDocument/documentHighlight" when IsMcssDocument(parameters) =>
                    await McssDocumentHighlightAsync(parameters, cancellationToken),
                "textDocument/prepareRename" when IsMcssDocument(parameters) => null,
                "textDocument/rename" when IsMcssDocument(parameters) => null,
                "textDocument/codeAction" when IsMcssDocument(parameters) => new JsonArray(),
                "textDocument/selectionRange" when IsMcssDocument(parameters) =>
                    McssSelectionRanges(parameters),
                "textDocument/documentSymbol" when IsMcssDocument(parameters) =>
                    await McssDocumentSymbolsAsync(parameters, cancellationToken),
                "textDocument/semanticTokens/full" when IsMcssDocument(parameters) =>
                    await McssSemanticTokensAsync(parameters, cancellationToken),
                "textDocument/completion" => await CompletionAsync(parameters, cancellationToken),
                "textDocument/hover" => await HoverAsync(parameters, cancellationToken),
                "textDocument/definition" => await DefinitionAsync(parameters, cancellationToken),
                "textDocument/references" => await ReferencesAsync(parameters, cancellationToken),
                "textDocument/documentHighlight" => await DocumentHighlightAsync(parameters, cancellationToken),
                "textDocument/prepareRename" => await PrepareRenameAsync(parameters, cancellationToken),
                "textDocument/rename" => await RenameAsync(parameters, cancellationToken),
                "textDocument/codeAction" => await CodeActionsAsync(parameters, cancellationToken),
                "textDocument/selectionRange" => await SelectionRangeAsync(parameters, cancellationToken),
                "textDocument/documentSymbol" => await DocumentSymbolsAsync(parameters, cancellationToken),
                "textDocument/semanticTokens/full" => await SemanticTokensAsync(parameters, cancellationToken),
                "textDocument/diagnostic" => await DocumentDiagnosticAsync(parameters, cancellationToken),
                _ => null,
            };
            await SendResultAsync(id, result, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            await SendErrorAsync(id, -32603, exception.Message, cancellationToken);
        } finally {
            if (cancellation is not null) {
                var key = RequestKey(id);
                _requests.TryRemove(key, out _);
                cancellation.Dispose();
            }
        }
    }

    private void StartRequest(
        JsonElement id,
        string method,
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var key = RequestKey(id);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _requests[key] = cancellation;
        var task = HandleRequestAsync(
            id,
            method,
            parameters,
            cancellation.Token,
            cancellation);
        lock (_requestTasksLock) {
            _requestTasks.Add(task);
        }

        _ = task.ContinueWith(
            completed => {
                lock (_requestTasksLock) {
                    _requestTasks.Remove(completed);
                }

                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void HandleNotification(string method, JsonElement parameters) {
        switch (method) {
            case "$/cancelRequest":
                if (parameters.TryGetProperty("id", out var requestId) &&
                    _requests.TryGetValue(RequestKey(requestId), out var cancellation)) {
                    try {
                        cancellation.Cancel();
                    } catch (ObjectDisposedException) {
                    }
                }

                break;
            case "textDocument/didOpen":
                Open(parameters);
                break;
            case "textDocument/didChange":
                Change(parameters);
                break;
            case "textDocument/didSave":
                Save(parameters);
                break;
            case "textDocument/didClose":
                Close(parameters);
                break;
            case "melanopsin/preload":
                Preload(parameters);
                break;
            case "workspace/didChangeWatchedFiles":
                foreach (var change in parameters.GetProperty("changes").EnumerateArray()) {
                    if (change.TryGetProperty("uri", out var uri)) {
                        _workspace.Invalidate(UriToPath(uri.GetString() ?? string.Empty));
                    }
                }

                break;
            case "initialized":
                _ = SendLogMessageAsync("Language server initialized.");
                break;
        }
    }

    private void Preload(JsonElement parameters) {
        if (!parameters.TryGetProperty("paths", out var paths) ||
            paths.ValueKind != JsonValueKind.Array) {
            return;
        }

        var documentPaths = paths.EnumerateArray()
            .Select(static path => path.GetString())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path!))
            .Distinct(StringComparer.Ordinal)
            .Take(32)
            .ToArray();
        if (documentPaths.Length == 0) {
            return;
        }

        CancellationTokenSource cancellation;
        lock (_preloadLock) {
            if (_preloadCancellation is not null) {
                return;
            }

            cancellation = new CancellationTokenSource();
            _preloadCancellation = cancellation;
        }

        _ = PreloadProjectsAsync(documentPaths, cancellation);
    }

    private async Task PreloadProjectsAsync(
        IReadOnlyList<string> documentPaths,
        CancellationTokenSource cancellation) {
        try {
            var projects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in documentPaths) {
                var directory = Path.GetDirectoryName(path);
                if (directory is null || !projects.Add(directory)) {
                    continue;
                }

                try {
                    await _workspace.GetCompilationAsync(path, cancellation.Token);
                } catch (FileNotFoundException) {
                }

                if (projects.Count >= 8) {
                    break;
                }
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            Console.Error.WriteLine($"mxml preload: {exception}");
        } finally {
            lock (_preloadLock) {
                if (ReferenceEquals(_preloadCancellation, cancellation)) {
                    _preloadCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private Task SendLogMessageAsync(string message) {
        return _connection.SendAsync(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["method"] = "window/logMessage",
            ["params"] = new JsonObject {
                ["type"] = 3,
                ["message"] = message,
            },
        }, CancellationToken.None);
    }

    private static string RequestKey(JsonElement id) {
        return id.ValueKind switch {
            JsonValueKind.String => "s:" + id.GetString(),
            JsonValueKind.Number => "n:" + id.GetRawText(),
            _ => id.GetRawText(),
        };
    }

    private JsonObject Initialize(JsonElement parameters) {
        _ = parameters;
        return new JsonObject {
            ["capabilities"] = new JsonObject {
                ["positionEncoding"] = "utf-16",
                ["textDocumentSync"] = new JsonObject {
                    ["openClose"] = true,
                    ["change"] = 1,
                    ["save"] = new JsonObject { ["includeText"] = true },
                },
                ["completionProvider"] = new JsonObject {
                    ["triggerCharacters"] = new JsonArray(
                        "<", " ", "\"", "'", "{", "}", ":", ";", "@", "$", "#", ".", "="),
                },
                ["hoverProvider"] = true,
                ["definitionProvider"] = true,
                ["referencesProvider"] = true,
                ["documentHighlightProvider"] = true,
                ["renameProvider"] = new JsonObject { ["prepareProvider"] = true },
                ["codeActionProvider"] = new JsonObject {
                    ["codeActionKinds"] = new JsonArray("quickfix", "refactor.rewrite"),
                },
                ["selectionRangeProvider"] = true,
                ["documentSymbolProvider"] = true,
                ["diagnosticProvider"] = new JsonObject {
                    ["interFileDependencies"] = false,
                    ["workspaceDiagnostics"] = false,
                },
                ["semanticTokensProvider"] = new JsonObject {
                    ["legend"] = new JsonObject {
                        ["tokenTypes"] = TokenTypeArray(),
                        ["tokenModifiers"] = new JsonArray(),
                    },
                    ["full"] = true,
                },
            },
            ["serverInfo"] = new JsonObject {
                ["name"] = "Melanopsin Language Server",
                ["version"] = "1.0.0",
            },
        };
    }

    private JsonNode? Shutdown() {
        return null;
    }

    private static JsonArray TokenTypeArray() {
        var result = new JsonArray();
        foreach (var tokenType in TokenTypes) {
            result.Add(tokenType);
        }

        return result;
    }

    private async Task<JsonArray> CompletionAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        var items = MarkupLanguageService.GetCompletions(
            document.Path,
            document.Text,
            offset,
            compilation,
            cancellationToken);
        var (replaceStart, replaceEnd) = WordBounds(document.Text, offset);
        var range = PositionMapper.Range(
            document.Text,
            replaceStart,
            replaceEnd - replaceStart);
        var result = new JsonArray();
        foreach (var item in items) {
            var completion = new JsonObject {
                ["label"] = item.Label,
                ["kind"] = CompletionItemKind(item.Kind),
                ["detail"] = item.Detail,
                ["textEdit"] = new JsonObject {
                    ["range"] = JsonSerializer.SerializeToNode(range),
                    ["newText"] = item.InsertText,
                },
                ["insertTextFormat"] = item.IsSnippet ? 2 : 1,
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

    private async Task<JsonObject?> HoverAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        if (ClosingTagHover(document, offset) is { } tagHover) {
            return tagHover;
        }

        var hover = MarkupLanguageService.GetHover(
            document.Path,
            document.Text,
            offset,
            compilation,
            cancellationToken);
        return hover is null
            ? null
            : new JsonObject {
                ["contents"] = new JsonObject {
                    ["kind"] = "markdown",
                    ["value"] = hover.Markdown,
                },
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    hover.Offset,
                    hover.Length)),
            };
    }

    private async Task<JsonArray> DefinitionAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var offset = TextDocumentOffset(parameters, document);
        if (TagDefinition(document, offset) is { } tagLocation) {
            return [tagLocation];
        }

        var hover = MarkupLanguageService.GetHover(
            document.Path,
            document.Text,
            offset,
            compilation,
            cancellationToken);
        if (hover?.Definition is not { } target) {
            return new JsonArray();
        }

        var locations = SymbolLocator.FindLocations(
            compilation,
            target.TypeName,
            target.MemberName,
            target.AttributeName,
            cancellationToken);
        var result = new JsonArray();
        foreach (var location in locations) {
            // Mapped spans keep navigation inside generated code pointed at the source it was
            // generated from - a member declared in @code navigates into the .mxml.
            var span = location.GetMappedLineSpan();
            result.Add(new JsonObject {
                ["uri"] = PathToUri(span.Path),
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.RangePositions(
                    span.StartLinePosition.Line,
                    span.StartLinePosition.Character,
                    span.EndLinePosition.Line,
                    span.EndLinePosition.Character)),
            });
        }

        return result;
    }

    private async Task<JsonArray> DocumentSymbolsAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var analysis = MarkupLanguageService.Analyze(
            document.Path,
            document.Text,
            compilation,
            cancellationToken);
        var symbols = new JsonArray();
        foreach (var element in analysis.Elements) {
            symbols.Add(ElementSymbol(element, document.Text));
        }

        return symbols;
    }

    private async Task<JsonObject> SemanticTokensAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var (document, compilation) = await GetDocumentAsync(parameters, cancellationToken);
        var tokens = MarkupLanguageService.GetSemanticTokens(
            document.Path,
            document.Text,
            compilation,
            cancellationToken);
        var data = new JsonArray();
        var previousLine = 0;
        var previousCharacter = 0;
        foreach (var token in tokens.OrderBy(static token => token.Offset)) {
            var (line, character) = PositionMapper.ToPosition(document.Text, token.Offset);
            data.Add(line - previousLine);
            data.Add(line == previousLine ? character - previousCharacter : character);
            data.Add(token.Length);
            data.Add(TokenTypeIndex(token.Kind));
            data.Add(0);
            previousLine = line;
            previousCharacter = character;
        }

        return new JsonObject { ["data"] = data };
    }

    /// <summary>
    /// Maps a semantic token kind to its index in <see cref="TokenTypes"/>. The mapping is
    /// explicit so the legend and the enum can evolve independently.
    /// </summary>
    private static int TokenTypeIndex(MarkupSemanticTokenKind kind) {
        return kind switch {
            MarkupSemanticTokenKind.Element => 0,
            MarkupSemanticTokenKind.Component => 1,
            MarkupSemanticTokenKind.ClassName => 2,
            MarkupSemanticTokenKind.Property => 3,
            MarkupSemanticTokenKind.Event => 4,
            MarkupSemanticTokenKind.Keyword => 5,
            MarkupSemanticTokenKind.Variable => 6,
            MarkupSemanticTokenKind.Function => 7,
            MarkupSemanticTokenKind.EnumValue => 8,
            MarkupSemanticTokenKind.Number => 9,
            _ => 0,
        };
    }

    private void Open(JsonElement parameters) {
        var textDocument = parameters.GetProperty("textDocument");
        var uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
        var text = textDocument.GetProperty("text").GetString() ?? string.Empty;
        var version = textDocument.GetProperty("version").GetInt32();
        _documents.Open(uri, UriToPath(uri), text, version);
        ScheduleDiagnostics(uri);
    }

    private void Change(JsonElement parameters) {
        var textDocument = parameters.GetProperty("textDocument");
        var uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
        var version = textDocument.GetProperty("version").GetInt32();
        var changes = parameters.GetProperty("contentChanges");
        var text = changes[changes.GetArrayLength() - 1].GetProperty("text").GetString() ?? string.Empty;
        _documents.Update(uri, text, version);
        ScheduleDiagnostics(uri);
    }

    private void Save(JsonElement parameters) {
        var textDocument = parameters.GetProperty("textDocument");
        var uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
        if (textDocument.TryGetProperty("text", out var text)) {
            _documents.Update(uri, text.GetString() ?? string.Empty, int.MaxValue);
        }

        var path = UriToPath(uri);
        if (Path.GetExtension(path).Equals(".mres", StringComparison.OrdinalIgnoreCase)) {
            _workspace.Invalidate(path);
        }

        ScheduleDiagnostics(uri);
    }

    private void Close(JsonElement parameters) {
        var uri = TextDocumentUri(parameters);
        lock (_diagnosticsLock) {
            _diagnosticsCancellation?.Cancel();
            _diagnosticsCancellation = null;
        }

        _documents.Close(uri);
    }

    private async Task<(OpenDocument Document, Compilation Compilation)> GetDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var uri = TextDocumentUri(parameters);
        if (!_documents.TryGet(uri, out var document)) {
            throw new InvalidOperationException($"Document '{uri}' is not open.");
        }

        var compilation = await _workspace.GetCompilationAsync(document.Path, cancellationToken);
        return (document, compilation);
    }

    private static int TextDocumentOffset(JsonElement parameters, OpenDocument document) {
        var position = parameters.GetProperty("position");
        return PositionMapper.ToOffset(
            document.Text,
            position.GetProperty("line").GetInt32(),
            position.GetProperty("character").GetInt32());
    }

    private static string TextDocumentUri(JsonElement parameters) {
        return parameters.GetProperty("textDocument").GetProperty("uri").GetString() ??
               throw new InvalidOperationException("LSP text document URI is missing.");
    }

    private bool IsMcssDocument(JsonElement parameters) {
        if (!parameters.TryGetProperty("textDocument", out var textDocument) ||
            !textDocument.TryGetProperty("uri", out var uriElement) ||
            uriElement.GetString() is not { } uri ||
            !_documents.TryGet(uri, out var document)) {
            return false;
        }

        return IsMcssPath(document.Path);
    }

    private static bool IsMcssPath(string path) {
        return Path.GetExtension(path).Equals(".mcss", StringComparison.OrdinalIgnoreCase);
    }

    private static (int Start, int End) WordBounds(string text, int offset) {
        var start = Math.Min(offset, text.Length);
        var end = start;
        while (start > 0 && IsWordCharacter(text[start - 1])) {
            start--;
        }

        while (end < text.Length && IsWordCharacter(text[end])) {
            end++;
        }

        return (start, end);
    }

    private static bool IsWordCharacter(char value) {
        return char.IsLetterOrDigit(value) || value is '_' or '-' or '.';
    }

    private static JsonObject ElementSymbol(MarkupElementInfo element, string text) {
        var range = PositionMapper.Range(
            text,
            Math.Max(0, element.Offset - 1),
            element.NameLength + 1);
        var selectionRange = PositionMapper.Range(
            text,
            element.Offset,
            element.NameLength);
        var children = new JsonArray();
        foreach (var child in element.Children) {
            children.Add(ElementSymbol(child, text));
        }

        return new JsonObject {
            ["name"] = element.QualifiedName,
            ["detail"] = element.Control?.QualifiedName ?? "Unresolved element",
            ["kind"] = 5,
            ["range"] = JsonSerializer.SerializeToNode(range),
            ["selectionRange"] = JsonSerializer.SerializeToNode(selectionRange),
            ["children"] = children,
        };
    }

    private static int CompletionItemKind(MarkupCompletionKind kind) {
        return kind switch {
            MarkupCompletionKind.Class => 7,
            MarkupCompletionKind.Property => 10,
            MarkupCompletionKind.Field => 5,
            MarkupCompletionKind.Method => 2,
            MarkupCompletionKind.Event => 23,
            MarkupCompletionKind.Keyword => 14,
            MarkupCompletionKind.Value => 12,
            MarkupCompletionKind.Snippet => 15,
            _ => 1,
        };
    }

    private async Task SendResultAsync(
        JsonElement id,
        JsonNode? result,
        CancellationToken cancellationToken) {
        await _connection.SendAsync(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonSerializer.SerializeToNode(id),
            ["result"] = result,
        }, cancellationToken);
    }

    private async Task SendErrorAsync(
        JsonElement id,
        int code,
        string message,
        CancellationToken cancellationToken) {
        await _connection.SendAsync(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonSerializer.SerializeToNode(id),
            ["error"] = new JsonObject {
                ["code"] = code,
                ["message"] = message,
            },
        }, cancellationToken);
    }

    private static string UriToPath(string uri) {
        var path = new Uri(uri).LocalPath;
        return OperatingSystem.IsWindows() && path.Length >= 3 && path[0] == '/' && path[2] == ':'
            ? path.Substring(1)
            : path;
    }

    private static string PathToUri(string path) {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }
}
