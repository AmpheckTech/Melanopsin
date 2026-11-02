using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// Tokenizes embedded C# - an interpolation body or an event handler body - into the member
/// references and numeric literals worth colouring. Used when the compilation cannot answer; symbol
/// classification takes over otherwise.
/// </summary>
internal static class MarkupCodeScanner {
    public static IEnumerable<MarkupSemanticToken> Scan(
        string source,
        int start,
        int end,
        IReadOnlyCollection<string> memberNames) {
        var from = Math.Max(start, 0);
        var position = from;
        var limit = Math.Min(end, source.Length);
        while (position < limit) {
            if (IsAsciiDigit(source[position]) &&
                (position == from || !IsIdentifierPart(source[position - 1]))) {
                var numberStart = position;
                while (position < limit && IsAsciiDigit(source[position])) {
                    position++;
                }

                if (position < limit && source[position] == '.' &&
                    position + 1 < limit && IsAsciiDigit(source[position + 1])) {
                    position++;
                    while (position < limit && IsAsciiDigit(source[position])) {
                        position++;
                    }
                }

                yield return new MarkupSemanticToken(
                    numberStart,
                    position - numberStart,
                    MarkupSemanticTokenKind.Number);
                continue;
            }

            if (!IsIdentifierStart(source[position])) {
                position++;
                continue;
            }

            var tokenStart = position++;
            while (position < limit && IsIdentifierPart(source[position])) {
                position++;
            }

            var name = source.Substring(tokenStart, position - tokenStart);
            if (!memberNames.Contains(name)) {
                continue;
            }

            var probe = position;
            while (probe < limit && char.IsWhiteSpace(source[probe])) {
                probe++;
            }

            yield return new MarkupSemanticToken(
                tokenStart,
                position - tokenStart,
                probe < limit && source[probe] == '('
                    ? MarkupSemanticTokenKind.Function
                    : MarkupSemanticTokenKind.Variable);
        }
    }

    public static IEnumerable<MarkupSemanticToken> ScanInterpolations(
        string source,
        IReadOnlyCollection<string> memberNames) {
        var cursor = 0;
        while (cursor < source.Length) {
            var start = source.IndexOf("{{", cursor, StringComparison.Ordinal);
            if (start < 0) {
                yield break;
            }

            var end = source.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0) {
                end = source.Length;
            }

            foreach (var token in Scan(source, start + 2, end, memberNames)) {
                yield return token;
            }

            cursor = Math.Min(source.Length, end + 2);
        }
    }

    private static bool IsIdentifierStart(char value) {
        return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value) {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static bool IsAsciiDigit(char value) {
        return value is >= '0' and <= '9';
    }
}
