// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Text.Json.Nodes;
using Melanopsin.UI.LanguageServer.LanguageFeatures;
using Melanopsin.UI.Markup.Compiler.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.LanguageServer.Protocol;

/// <summary>
/// The symbol features of the C# a document declares - <c>@code</c> blocks, <c>{{ }}</c> expressions
/// and event handler attributes. Requests answer against the tree emitted for the editor buffer, so
/// navigation and rename stay in the file being edited.
/// </summary>
internal sealed partial class LspServer {
    private readonly record struct CodeSpan(int Offset, int Length);

    /// <summary>
    /// The symbol of the C# identifier under an offset. <c>false</c> means the offset holds markup,
    /// so the caller answers with the markup features instead.
    /// </summary>
    private static bool TryCodeSymbol(
        Compilation compilation,
        OpenDocument document,
        int offset,
        CancellationToken cancellationToken,
        out ISymbol symbol,
        out CodeSpan span) {
        symbol = null!;
        span = default;
        if (CodeTree(compilation, document, cancellationToken) is not { } tree) {
            return false;
        }

        var model = compilation.GetSemanticModel(tree);
        foreach (var token in tree.GetRoot(cancellationToken).DescendantTokens()) {
            if (!token.IsKind(SyntaxKind.IdentifierToken) ||
                !MappedSpan(token, document, out var mapped) ||
                offset < mapped.Offset ||
                offset >= mapped.Offset + mapped.Length) {
                continue;
            }

            if (SymbolOf(model, token, cancellationToken) is not { } found) {
                return false;
            }

            symbol = found;
            span = mapped;
            return true;
        }

        return TryEmbeddedSymbol(compilation, document, tree, offset, cancellationToken, out symbol, out span);
    }

    /// <summary>Every span of the buffer that refers to one symbol.</summary>
    private static IReadOnlyList<CodeSpan> CodeSpansOf(
        Compilation compilation,
        OpenDocument document,
        ISymbol symbol,
        CancellationToken cancellationToken) {
        if (CodeTree(compilation, document, cancellationToken) is not { } tree) {
            return [];
        }

        var model = compilation.GetSemanticModel(tree);
        var spans = new List<CodeSpan>();
        foreach (var token in tree.GetRoot(cancellationToken).DescendantTokens()) {
            if (token.IsKind(SyntaxKind.IdentifierToken) &&
                MappedSpan(token, document, out var mapped) &&
                SymbolOf(model, token, cancellationToken) is { } candidate &&
                SymbolEqualityComparer.Default.Equals(candidate, symbol)) {
                spans.Add(mapped);
            }
        }

        foreach (var span in EmbeddedSpansOf(compilation, document, tree, model, symbol, cancellationToken)) {
            spans.Add(span);
        }

        return spans
            .Distinct()
            .OrderBy(static span => span.Offset)
            .ToArray();
    }

    private static JsonArray CodeLocations(
        OpenDocument document,
        IReadOnlyList<CodeSpan> spans) {
        var result = new JsonArray();
        foreach (var span in spans) {
            result.Add(Location(document.Uri, document.Text, span.Offset, span.Length));
        }

        return result;
    }

    private static JsonArray CodeHighlights(
        OpenDocument document,
        IReadOnlyList<CodeSpan> spans) {
        var result = new JsonArray();
        foreach (var span in spans) {
            AddHighlight(result, document.Text, span.Offset, span.Length);
        }

        return result;
    }

    private static SyntaxTree? CodeTree(
        Compilation compilation,
        OpenDocument document,
        CancellationToken cancellationToken) {
        return MarkupLanguageService.FindComponentTree(
            compilation,
            document.Path,
            buffered: true,
            cancellationToken);
    }

    /// <summary>The markup span a token occupies, or <c>false</c> for generated code.</summary>
    private static bool MappedSpan(
        SyntaxToken token,
        OpenDocument document,
        out CodeSpan span) {
        span = default;
        var mapped = token.GetLocation().GetMappedLineSpan();
        if (!string.Equals(mapped.Path, document.Path, StringComparison.Ordinal)) {
            return false;
        }

        var start = PositionMapper.ToOffset(
            document.Text,
            mapped.StartLinePosition.Line,
            mapped.StartLinePosition.Character);
        var end = PositionMapper.ToOffset(
            document.Text,
            mapped.EndLinePosition.Line,
            mapped.EndLinePosition.Character);
        span = new CodeSpan(start, end - start);
        return true;
    }

    private static ISymbol? SymbolOf(
        SemanticModel model,
        SyntaxToken token,
        CancellationToken cancellationToken) {
        var node = token.Parent;
        return node is null
            ? null
            : model.GetDeclaredSymbol(node, cancellationToken) ??
              model.GetSymbolInfo(node, cancellationToken).Symbol;
    }

    /// <summary>
    /// Resolves an offset inside <c>{{ }}</c> or an event handler attribute. Those expressions are
    /// copied into the generated code verbatim but carry no <c>#line</c>, so the snippet around the
    /// caret is located in that code and the token there answers for it.
    /// </summary>
    private static bool TryEmbeddedSymbol(
        Compilation compilation,
        OpenDocument document,
        SyntaxTree tree,
        int offset,
        CancellationToken cancellationToken,
        out ISymbol symbol,
        out CodeSpan span) {
        symbol = null!;
        span = default;
        if (!IdentifierAt(document.Text, offset, out var start, out var length)) {
            return false;
        }

        var model = compilation.GetSemanticModel(tree);
        var generated = tree.GetText(cancellationToken).ToString();
        foreach (var block in EmbeddedSpans(compilation, document, cancellationToken)) {
            if (start < block.Offset || start >= block.Offset + block.Length) {
                continue;
            }

            var snippet = document.Text.Substring(block.Offset, block.Length);
            if (FindEmbeddedToken(
                    tree,
                    model,
                    generated,
                    snippet,
                    start - block.Offset,
                    length,
                    cancellationToken) is { } found) {
                symbol = found.Symbol;
                span = new CodeSpan(start, length);
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<CodeSpan> EmbeddedSpansOf(
        Compilation compilation,
        OpenDocument document,
        SyntaxTree tree,
        SemanticModel model,
        ISymbol symbol,
        CancellationToken cancellationToken) {
        var generated = tree.GetText(cancellationToken).ToString();
        foreach (var block in EmbeddedSpans(compilation, document, cancellationToken)) {
            var snippet = document.Text.Substring(block.Offset, block.Length);
            if (snippet.IndexOf(symbol.Name, StringComparison.Ordinal) < 0) {
                continue;
            }

            foreach (var (start, length) in Identifiers(snippet)) {
                if (string.CompareOrdinal(snippet, start, symbol.Name, 0, length) != 0 ||
                    length != symbol.Name.Length) {
                    continue;
                }

                if (FindEmbeddedToken(
                        tree,
                        model,
                        generated,
                        snippet,
                        start,
                        length,
                        cancellationToken) is { Symbol: var found } &&
                    SymbolEqualityComparer.Default.Equals(found, symbol)) {
                    yield return new CodeSpan(block.Offset + start, length);
                }
            }
        }
    }

    /// <summary>
    /// The token in the generated code that a verbatim copy of a markup expression produced. The
    /// span has to line up exactly, and the snippet may occur more than once, so every occurrence is
    /// tried until one holds an identifier.
    /// </summary>
    private static (ISymbol Symbol, SyntaxToken Token)? FindEmbeddedToken(
        SyntaxTree tree,
        SemanticModel model,
        string generated,
        string snippet,
        int relative,
        int length,
        CancellationToken cancellationToken) {
        var root = tree.GetRoot(cancellationToken);
        var search = 0;
        while (search <= generated.Length - snippet.Length) {
            var index = generated.IndexOf(snippet, search, StringComparison.Ordinal);
            if (index < 0) {
                return null;
            }

            search = index + 1;
            var position = index + relative;
            var token = root.FindToken(position);
            if (token.IsKind(SyntaxKind.IdentifierToken) &&
                token.SpanStart == position &&
                token.Span.Length == length &&
                SymbolOf(model, token, cancellationToken) is { } symbol) {
                return (symbol, token);
            }
        }

        return null;
    }

    private static IReadOnlyList<MarkupCodeBlockInfo> EmbeddedSpans(
        Compilation compilation,
        OpenDocument document,
        CancellationToken cancellationToken) {
        return MarkupLanguageService.GetEmbeddedCodeSpans(
            document.Path,
            document.Text,
            compilation,
            cancellationToken);
    }

    /// <summary>The identifiers of a snippet, in order, as offsets relative to its start.</summary>
    private static IEnumerable<(int Start, int Length)> Identifiers(string text) {
        var index = 0;
        while (index < text.Length) {
            var current = text[index];
            if (!IsIdentifierStart(current)) {
                index++;
                continue;
            }

            var start = index;
            index++;
            while (index < text.Length && IsIdentifierPart(text[index])) {
                index++;
            }

            yield return (start, index - start);
        }
    }

    private static bool IdentifierAt(string text, int offset, out int start, out int length) {
        start = 0;
        length = 0;
        if (offset < 0 || offset >= text.Length || !IsIdentifierPart(text[offset])) {
            return false;
        }

        start = offset;
        while (start > 0 && IsIdentifierPart(text[start - 1])) {
            start--;
        }

        var end = offset;
        while (end + 1 < text.Length && IsIdentifierPart(text[end + 1])) {
            end++;
        }

        length = end - start + 1;
        return IsIdentifierStart(text[start]);
    }

    private static bool IsIdentifierStart(char character) {
        return character == '_' || char.IsLetter(character);
    }

    private static bool IsIdentifierPart(char character) {
        return character == '_' || char.IsLetterOrDigit(character);
    }
}
