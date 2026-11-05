// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class MarkupLanguageService {
    private static readonly string[] Directives =
        ["if", "else", "for", "empty", "switch", "case", "default", "code"];

    private static IReadOnlyList<MarkupSemanticToken> GetSemanticTokens(
        string path,
        string source,
        MarkupAnalysis analysis,
        Compilation compilation,
        CancellationToken cancellationToken) {
        var tokens = new List<MarkupSemanticToken>();
        var handlerSpans = new List<(int Start, int End)>();
        var component = FindComponent(analysis, compilation, cancellationToken);
        foreach (var element in analysis.Elements.SelectMany(Flatten)) {
            tokens.Add(new MarkupSemanticToken(
                element.Offset,
                element.NameLength,
                element.Control?.IsComponent == true
                    ? MarkupSemanticTokenKind.Component
                    : MarkupSemanticTokenKind.Element));
            if (element.Control is null ||
                !TryFindOpenTag(source, element.Offset, out var tag)) {
                continue;
            }

            foreach (var attributeSpan in tag.Attributes) {
                var nameLength = attributeSpan.NameEnd - attributeSpan.NameStart;
                if (nameLength <= 0) {
                    continue;
                }

                var name = source.Substring(attributeSpan.NameStart, nameLength);
                if (name[0] == '@') {
                    // Directives are tokenized as keywords below.
                    continue;
                }

                var isEvent = IsEventHandler(element.Control, source, attributeSpan);
                tokens.Add(new MarkupSemanticToken(
                    attributeSpan.NameStart,
                    nameLength,
                    isEvent ? MarkupSemanticTokenKind.Event : MarkupSemanticTokenKind.Property));

                if (attributeSpan.ValueStart < 0) {
                    continue;
                }

                if (isEvent) {
                    // Handler values are C# statements, compiled into the event lambda. They are
                    // scanned at the end, after the compilation has had its say.
                    handlerSpans.Add((attributeSpan.ValueStart, attributeSpan.ValueEnd));
                    continue;
                }

                var schema = element.Control.Attributes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.Ordinal));
                if (schema is not null) {
                    AddValueToken(source, attributeSpan, name, schema, tokens);
                }
            }
        }

        tokens.AddRange(GetDirectiveTokens(source));
        tokens.AddRange(GetCodeTokens(path, source, analysis, compilation, component, cancellationToken));
        if (component is not null) {
            // The compilation classifies the copied snippets; this is the fallback for spans it
            // does not see, such as a handler the generator had to rewrite.
            var names = MemberNames(component);
            foreach (var (start, end) in handlerSpans) {
                tokens.AddRange(MarkupCodeScanner.Scan(source, start, end, names));
            }

            tokens.AddRange(MarkupCodeScanner.ScanInterpolations(source, names));
        }

        return Normalize(tokens);
    }

    private static void AddValueToken(
        string source,
        AttributeSpan attributeSpan,
        string name,
        MarkupAttributeInfo schema,
        List<MarkupSemanticToken> tokens) {
        var (text, offset) = Trim(source, attributeSpan.ValueStart, attributeSpan.ValueEnd);
        if (text.Length == 0 ||
            text.Contains("{{", StringComparison.Ordinal)) {
            return;
        }

        if (string.Equals(name, "class", StringComparison.Ordinal)) {
            AddClassNameTokens(text, offset, tokens);
            return;
        }

        var isAllowedValue = schema.AllowedValues.Any(value =>
            string.Equals(value, text, StringComparison.OrdinalIgnoreCase));
        if (isAllowedValue ||
            (schema.ValueKind == "Boolean" &&
             (text == "true" || text == "false"))) {
            tokens.Add(new MarkupSemanticToken(
                offset,
                text.Length,
                MarkupSemanticTokenKind.EnumValue));
            return;
        }

        if (NumericLiteralLength(text) is { } numberLength) {
            tokens.Add(new MarkupSemanticToken(
                offset,
                numberLength,
                MarkupSemanticTokenKind.Number));
        }
    }

    private static void AddClassNameTokens(
        string text,
        int offset,
        List<MarkupSemanticToken> tokens) {
        var index = 0;
        while (index < text.Length) {
            while (index < text.Length && char.IsWhiteSpace(text[index])) {
                index++;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index])) {
                index++;
            }

            if (index > start) {
                tokens.Add(new MarkupSemanticToken(
                    offset + start,
                    index - start,
                    MarkupSemanticTokenKind.ClassName));
            }
        }
    }

    private static (string Text, int Offset) Trim(string source, int start, int end) {
        var offset = Math.Max(start, 0);
        var limit = Math.Min(end, source.Length);
        while (offset < limit && char.IsWhiteSpace(source[offset])) {
            offset++;
        }

        while (limit > offset && char.IsWhiteSpace(source[limit - 1])) {
            limit--;
        }

        return (source.Substring(offset, limit - offset), offset);
    }

    /// <summary>Returns the length of a leading number, length or percentage literal.</summary>
    private static int? NumericLiteralLength(string text) {
        var index = 0;
        if (index < text.Length && text[index] is '+' or '-') {
            index++;
        }

        var digits = 0;
        while (index < text.Length && IsAsciiDigit(text[index])) {
            index++;
            digits++;
        }

        if (index < text.Length && text[index] == '.') {
            var fraction = index + 1;
            while (fraction < text.Length && IsAsciiDigit(text[fraction])) {
                fraction++;
            }

            if (fraction > index + 1) {
                digits += fraction - index - 1;
                index = fraction;
            }
        }

        if (digits == 0) {
            return null;
        }

        if (index < text.Length && text[index] == '%') {
            index++;
        }
        else {
            while (index < text.Length && IsAsciiLetter(text[index])) {
                index++;
            }
        }

        return index == text.Length ? index : null;
    }

    private static IReadOnlyList<MarkupSemanticToken> Normalize(
        List<MarkupSemanticToken> tokens) {
        var accepted = new List<MarkupSemanticToken>();
        foreach (var token in tokens
                     .Where(static token => token.Length > 0)
                     .GroupBy(static token => (token.Offset, token.Length, token.Kind))
                     .Select(static group => group.First())
                     .OrderBy(static token => token.Offset)
                     .ThenByDescending(static token => token.Length)) {
            // Clients reject overlapping tokens, so drop any token covered by an earlier one.
            if (accepted.Any(existing =>
                    token.Offset < existing.Offset + existing.Length &&
                    existing.Offset < token.Offset + token.Length)) {
                continue;
            }

            accepted.Add(token);
        }

        return accepted;
    }

    private static IEnumerable<MarkupSemanticToken> GetDirectiveTokens(string source) {
        for (var index = 0; index < source.Length - 1; index++) {
            if (source[index] != '@') {
                continue;
            }

            var start = index + 1;
            var end = start;
            while (end < source.Length && char.IsLetter(source[end])) {
                end++;
            }

            if (Directives.Contains(
                    source.Substring(start, end - start),
                    StringComparer.Ordinal)) {
                yield return new MarkupSemanticToken(
                    index,
                    end - index,
                    MarkupSemanticTokenKind.Keyword);
            }
        }
    }

    /// <summary>
    /// Classifies the C# inside <c>@code</c> blocks by what each token binds to. A document outside a
    /// compiled project falls back to matching names against the component's members.
    /// </summary>
    private static IEnumerable<MarkupSemanticToken> GetCodeTokens(
        string path,
        string source,
        MarkupAnalysis analysis,
        Compilation compilation,
        INamedTypeSymbol? component,
        CancellationToken cancellationToken) {
        if (MarkupComponentTrees.Find(compilation, path, buffered: false, cancellationToken)
            is { } tree) {
            var model = compilation.GetSemanticModel(tree);
            foreach (var (token, offset, length) in MarkupComponentTrees.DocumentTokens(
                         tree,
                         path,
                         source,
                         cancellationToken)) {
                if (ClassifyToken(model, token, cancellationToken) is { } kind) {
                    yield return new MarkupSemanticToken(offset, length, kind);
                }
            }

            yield break;
        }

        foreach (var block in analysis.CodeBlocks) {
            var start = Math.Min(block.Offset, source.Length);
            var end = Math.Min(block.Offset + block.Length, source.Length);
            foreach (var token in MarkupCodeScanner.Scan(
                         source,
                         start,
                         end,
                         MemberNames(component, includePrivate: true))) {
                yield return token;
            }
        }
    }

    /// <summary>The markup kind of one C# token, or <c>null</c> when nothing colours it.</summary>
    private static MarkupSemanticTokenKind? ClassifyToken(
        SemanticModel model,
        SyntaxToken token,
        CancellationToken cancellationToken) {
        if (token.IsKind(SyntaxKind.IdentifierToken)) {
            if (SyntaxFacts.IsContextualKeyword(token.Kind())) {
                return MarkupSemanticTokenKind.Keyword;
            }

            return SymbolOf(model, token, cancellationToken) switch {
                INamedTypeSymbol => MarkupSemanticTokenKind.Component,
                INamespaceSymbol or ITypeParameterSymbol => MarkupSemanticTokenKind.Element,
                IMethodSymbol => MarkupSemanticTokenKind.Function,
                IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } =>
                    MarkupSemanticTokenKind.EnumValue,
                IPropertySymbol or IFieldSymbol or IEventSymbol => MarkupSemanticTokenKind.Property,
                ILocalSymbol or IParameterSymbol => MarkupSemanticTokenKind.Variable,
                _ => (MarkupSemanticTokenKind?)null,
            };
        }

        if (SyntaxFacts.IsKeywordKind(token.Kind())) {
            return MarkupSemanticTokenKind.Keyword;
        }

        return token.Kind() switch {
            SyntaxKind.NumericLiteralToken => MarkupSemanticTokenKind.Number,
            _ => null,
        };
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
}
