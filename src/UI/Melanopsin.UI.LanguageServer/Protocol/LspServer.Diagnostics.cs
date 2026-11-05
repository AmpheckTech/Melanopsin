// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;
using Melanopsin.UI.LanguageServer.LanguageFeatures;
using Melanopsin.UI.LanguageServer.Workspace;
using Melanopsin.UI.Markup.Compiler.Language;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.LanguageServer.Protocol;

/// <summary>
/// Markup diagnostics and the C# diagnostics of the code a document declares. Both are published
/// after an edit and also served on request, so clients that pull diagnostics see the same list.
/// </summary>
internal sealed partial class LspServer {
    private void ScheduleDiagnostics(string uri) {
        CancellationTokenSource cancellation;
        lock (_diagnosticsLock) {
            _diagnosticsCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            _diagnosticsCancellation = cancellation;
        }

        _ = PublishDiagnosticsAfterDelayAsync(uri, cancellation);
    }

    private async Task PublishDiagnosticsAfterDelayAsync(
        string uri,
        CancellationTokenSource cancellation) {
        try {
            await Task.Delay(200, cancellation.Token);
            await PublishDiagnosticsAsync(uri, cancellation.Token);
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            Console.Error.WriteLine($"language diagnostics: {exception}");
        } finally {
            lock (_diagnosticsLock) {
                if (ReferenceEquals(_diagnosticsCancellation, cancellation)) {
                    _diagnosticsCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private async Task PublishDiagnosticsAsync(string uri, CancellationToken cancellationToken) {
        if (!_documents.TryGet(uri, out var document)) {
            return;
        }

        await _connection.SendAsync(new JsonObject {
            ["jsonrpc"] = "2.0",
            ["method"] = "textDocument/publishDiagnostics",
            ["params"] = new JsonObject {
                ["uri"] = uri,
                ["version"] = document.Version,
                ["diagnostics"] = await DiagnosticsAsync(document, cancellationToken),
            },
        }, cancellationToken);
    }

    private async Task<JsonObject> DocumentDiagnosticAsync(
        JsonElement parameters,
        CancellationToken cancellationToken) {
        var uri = TextDocumentUri(parameters);
        if (!_documents.TryGet(uri, out var document)) {
            throw new InvalidOperationException($"Document '{uri}' is not open.");
        }

        return new JsonObject {
            ["kind"] = "full",
            ["items"] = await DiagnosticsAsync(document, cancellationToken),
        };
    }

    private async Task<JsonArray> DiagnosticsAsync(
        OpenDocument document,
        CancellationToken cancellationToken) {
        var diagnostics = new JsonArray();
        try {
            if (IsMcssPath(document.Path)) {
                return await McssDiagnosticsAsync(document, cancellationToken);
            }

            var compilation = await _workspace.GetCompilationAsync(
                document.Path,
                cancellationToken);
            var analysis = MarkupLanguageService.Analyze(
                document.Path,
                document.Text,
                compilation,
                cancellationToken);
            foreach (var diagnostic in analysis.Diagnostics) {
                diagnostics.Add(new JsonObject {
                    ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                        document.Text,
                        diagnostic.Offset,
                        diagnostic.Length)),
                    ["severity"] = 1,
                    ["source"] = "mxml",
                    ["code"] = diagnostic.Code,
                    ["message"] = diagnostic.Message,
                });
            }

            foreach (var diagnostic in CodeDiagnostics(document, compilation, cancellationToken)) {
                diagnostics.Add(diagnostic);
            }
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            var language = IsMcssPath(document.Path) ? "mcss" : "mxml";
            Console.Error.WriteLine($"{language} analysis: {exception}");
            diagnostics.Add(new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(document.Text, 0, 1)),
                ["severity"] = 1,
                ["source"] = language,
                ["code"] = language == "mcss" ? "MCSS-LSP" : "MXML-LSP",
                ["message"] = exception.Message,
            });
        }

        return diagnostics;
    }

    private async Task<JsonArray> McssDiagnosticsAsync(
        OpenDocument document,
        CancellationToken cancellationToken) {
        var sources = await _workspace.GetStylesheetsAsync(document.Path, cancellationToken);
        if (!sources.Any(source => string.Equals(source.Path, document.Path, StringComparison.Ordinal))) {
            sources = sources.Append(new McssSourceFile(document.Path, document.Text)).ToArray();
        }

        var analysis = McssLanguageService.Analyze(document.Path, document.Text, sources);
        var result = new JsonArray();
        foreach (var diagnostic in analysis.Diagnostics) {
            result.Add(new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    diagnostic.Offset,
                    diagnostic.Length)),
                ["severity"] = 1,
                ["source"] = "mcss",
                ["code"] = diagnostic.Code,
                ["message"] = diagnostic.Message,
            });
        }

        return result;
    }

    /// <summary>
    /// The C# diagnostics of a document's <c>@code</c> blocks. They are read from the tree emitted
    /// for the buffer, and only the spans that <c>#line</c> maps back into the markup are reported.
    /// </summary>
    private static IEnumerable<JsonObject> CodeDiagnostics(
        OpenDocument document,
        Compilation compilation,
        CancellationToken cancellationToken) {
        var tree = MarkupLanguageService.FindComponentTree(
            compilation,
            document.Path,
            buffered: true,
            cancellationToken);
        if (tree is null) {
            yield break;
        }

        var model = compilation.GetSemanticModel(tree);
        foreach (var diagnostic in model.GetDiagnostics(cancellationToken: cancellationToken)) {
            if (diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning) ||
                !diagnostic.Location.IsInSource) {
                continue;
            }

            var span = diagnostic.Location.GetMappedLineSpan();
            if (!string.Equals(span.Path, document.Path, StringComparison.Ordinal)) {
                continue;
            }

            var start = PositionMapper.ToOffset(
                document.Text,
                span.StartLinePosition.Line,
                span.StartLinePosition.Character);
            var end = PositionMapper.ToOffset(
                document.Text,
                span.EndLinePosition.Line,
                span.EndLinePosition.Character);
            yield return new JsonObject {
                ["range"] = JsonSerializer.SerializeToNode(PositionMapper.Range(
                    document.Text,
                    start,
                    end - start)),
                ["severity"] = diagnostic.Severity == DiagnosticSeverity.Error ? 1 : 2,
                ["source"] = "c#",
                ["code"] = diagnostic.Id,
                ["message"] = diagnostic.GetMessage(),
            };
        }
    }
}
