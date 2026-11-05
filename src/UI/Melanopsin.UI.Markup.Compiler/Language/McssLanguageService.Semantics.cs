// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Styling;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class McssLanguageService {
    public static IReadOnlyList<McssSemanticToken> GetSemanticTokens(
        McssAnalysis analysis,
        string source) {
        var sheet = CssParser.Parse(analysis.Path, source);
        var tokens = new List<McssSemanticToken>();
        AddComments(source, tokens);
        AddDirectives(source, tokens);

        foreach (var import in sheet.Imports) {
            AddTokensIn(source, import.Offset, import.Length, McssSemanticTokenKind.Value, tokens);
            var at = source.LastIndexOf('@', Math.Max(0, import.Offset - 1));
            if (at >= 0 && source.AsSpan(at, Math.Min(7, source.Length - at))
                    .Equals("@import", StringComparison.OrdinalIgnoreCase)) {
                tokens.Add(new McssSemanticToken(at, 7, McssSemanticTokenKind.Directive));
            }
        }

        foreach (var rule in sheet.Rules.GroupBy(static item => item.Offset)) {
            AddSelectorTokens(source, rule.Key, tokens);
            foreach (var declaration in rule.SelectMany(static item => item.Declarations)
                         .GroupBy(static item => item.Offset)
                         .Select(static group => group.First())) {
                AddDeclarationTokens(source, declaration, tokens);
            }
        }

        foreach (var slot in sheet.Slots) {
            var nameOffset = TokenNameOffset(source, slot.Offset);
            Add(source, nameOffset, slot.Name.Length, McssSemanticTokenKind.Variable, tokens);
            var colon = source.LastIndexOf(':', Math.Max(0, slot.Offset - 1));
            var equal = colon < 0
                ? -1
                : source.IndexOf('=', colon + 1, Math.Max(0, slot.Offset - colon - 1));
            if (equal >= 0) {
                var typeStart = SkipWhitespace(source, equal + 1);
                Add(source, typeStart, slot.TypeName.Length, McssSemanticTokenKind.Type, tokens);
                var valueStart = SkipWhitespace(source, typeStart + slot.TypeName.Length);
                Add(source, valueStart, slot.Value.Length, McssSemanticTokenKind.Value, tokens);
            }
        }

        return Normalize(source, tokens);
    }

    private static void AddSelectorTokens(
        string source,
        int selectorOffset,
        List<McssSemanticToken> tokens) {
        if (selectorOffset < 0 || selectorOffset >= source.Length) {
            return;
        }

        var brace = source.IndexOf('{', selectorOffset);
        if (brace < 0) {
            return;
        }

        var index = selectorOffset;
        while (index < brace) {
            if (StartsComment(source, index)) {
                index = SkipCommentAt(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipStringAt(source, index);
                continue;
            }

            if (!IsSelectorIdentifierStart(source[index])) {
                index++;
                continue;
            }

            var start = index++;
            while (index < brace && IsSelectorIdentifierPart(source[index])) {
                index++;
            }

            var kind = McssSemanticTokenKind.Type;
            if (start > selectorOffset && source[start - 1] == '.') {
                kind = McssSemanticTokenKind.Class;
            } else if (start > selectorOffset && source[start - 1] == '#') {
                kind = McssSemanticTokenKind.Class;
            } else if (start > selectorOffset &&
                       (source[start - 1] == ':' ||
                        start > 1 && source[start - 1] == ':' && source[start - 2] == ':')) {
                kind = McssSemanticTokenKind.State;
            }

            Add(source, start, index - start, kind, tokens);
        }
    }

    private static void AddDeclarationTokens(
        string source,
        CssDeclaration declaration,
        List<McssSemanticToken> tokens) {
        var nameOffset = SkipWhitespace(source, declaration.Offset);
        Add(source, nameOffset, declaration.Name.Length, McssSemanticTokenKind.Property, tokens);
        AddValueTokens(source, declaration.ValueOffset, declaration.Length, tokens);
    }

    private static void AddValueTokens(
        string source,
        int offset,
        int length,
        List<McssSemanticToken> tokens) {
        var end = Math.Min(source.Length, offset + Math.Max(0, length));
        var index = Math.Min(Math.Max(offset, 0), source.Length);
        while (index < end) {
            if (StartsComment(source, index)) {
                index = SkipCommentAt(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                var stringEnd = SkipStringAt(source, index);
                Add(source, index, stringEnd - index, McssSemanticTokenKind.Value, tokens);
                index = stringEnd;
                continue;
            }

            if (source[index] == '$' && index + 1 < end && IsMcssIdentifierStart(source[index + 1])) {
                var start = index++;
                while (index < end && IsMcssIdentifierPart(source[index])) {
                    index++;
                }

                Add(source, start, index - start, McssSemanticTokenKind.Variable, tokens);
                continue;
            }

            if (char.IsDigit(source[index]) ||
                source[index] == '.' && index + 1 < end && char.IsDigit(source[index + 1])) {
                var start = index++;
                while (index < end && (char.IsLetterOrDigit(source[index]) ||
                                       source[index] is '.' or '%' or '-' or '+')) {
                    index++;
                }

                Add(source, start, index - start, McssSemanticTokenKind.Number, tokens);
                continue;
            }

            if (source[index] == '#') {
                var start = index++;
                while (index < end && char.IsLetterOrDigit(source[index])) {
                    index++;
                }

                Add(source, start, index - start, McssSemanticTokenKind.Value, tokens);
                continue;
            }

            if (IsMcssIdentifierStart(source[index])) {
                var start = index++;
                while (index < end && IsMcssIdentifierPart(source[index])) {
                    index++;
                }

                Add(source, start, index - start, McssSemanticTokenKind.Value, tokens);
                continue;
            }

            index++;
        }
    }

    private static void AddComments(string source, List<McssSemanticToken> tokens) {
        var index = 0;
        while (index < source.Length) {
            if (!StartsComment(source, index)) {
                if (source[index] is '\'' or '"') {
                    index = SkipStringAt(source, index);
                } else {
                    index++;
                }

                continue;
            }

            var end = SkipCommentAt(source, index);
            Add(source, index, end - index, McssSemanticTokenKind.Comment, tokens);
            index = end;
        }
    }

    private static void AddDirectives(string source, List<McssSemanticToken> tokens) {
        var index = 0;
        while (index < source.Length) {
            if (StartsComment(source, index)) {
                index = SkipCommentAt(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipStringAt(source, index);
                continue;
            }

            if (source[index] != '@') {
                index++;
                continue;
            }

            var start = index++;
            while (index < source.Length && IsMcssIdentifierPart(source[index])) {
                index++;
            }

            Add(source, start, index - start, McssSemanticTokenKind.Directive, tokens);
        }
    }

    private static IReadOnlyList<McssSemanticToken> Normalize(
        string source,
        List<McssSemanticToken> tokens) {
        var accepted = new List<McssSemanticToken>();
        foreach (var token in tokens
                     .Where(item => item.Offset >= 0 && item.Length > 0 &&
                                    item.Offset + item.Length <= source.Length)
                     .Distinct()
                     .OrderBy(static item => item.Offset)
                     .ThenByDescending(static item => item.Length)
                     .ThenBy(static item => item.Kind)) {
            if (accepted.Any(item =>
                    token.Offset < item.Offset + item.Length &&
                    item.Offset < token.Offset + token.Length)) {
                continue;
            }

            accepted.Add(token);
        }

        return accepted;
    }

    private static void AddTokensIn(
        string source,
        int offset,
        int length,
        McssSemanticTokenKind kind,
        List<McssSemanticToken> tokens) {
        var start = Math.Min(Math.Max(offset, 0), source.Length);
        var end = Math.Min(source.Length, start + Math.Max(0, length));
        Add(source, start, end - start, kind, tokens);
    }

    private static void Add(
        string source,
        int offset,
        int length,
        McssSemanticTokenKind kind,
        List<McssSemanticToken> tokens) {
        if (offset >= 0 && length > 0 && offset + length <= source.Length) {
            tokens.Add(new McssSemanticToken(offset, length, kind));
        }
    }

    private static int SkipWhitespace(string source, int index) {
        while (index < source.Length && char.IsWhiteSpace(source[index])) {
            index++;
        }

        return index;
    }

    private static int SkipStringAt(string source, int index) {
        var quote = source[index++];
        while (index < source.Length) {
            if (source[index] == '\\' && index + 1 < source.Length) {
                index += 2;
            } else if (source[index++] == quote) {
                break;
            }
        }

        return index;
    }

    private static int SkipCommentAt(string source, int index) {
        index += 2;
        while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/')) {
            index++;
        }

        return Math.Min(source.Length, index + 2);
    }

    private static bool IsSelectorIdentifierStart(char value) {
        return char.IsLetter(value) || value is '_' or '-';
    }

    private static bool IsSelectorIdentifierPart(char value) {
        return char.IsLetterOrDigit(value) || value is '_' or '-';
    }

    private static bool IsMcssIdentifierStart(char value) {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsMcssIdentifierPart(char value) {
        return char.IsLetterOrDigit(value) || value is '_' or '-';
    }
}
