// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// Lexes the raw tags of a document: names, extents and whether a tag closes or is self closing.
/// It never fails, so the editor keeps an index while a view is in the middle of being typed.
/// </summary>
internal static class MarkupTagScanner {
    public static List<MarkupTagToken> Scan(string source) {
        var tokens = new List<MarkupTagToken>();
        var position = 0;
        while (position < source.Length) {
            var start = source.IndexOf('<', position);
            if (start < 0) {
                break;
            }

            if (StartsWith(source, start, "<!--")) {
                position = SkipDelimited(source, start + 4, "-->");
                continue;
            }

            if (StartsWith(source, start, "<![CDATA[")) {
                position = SkipDelimited(source, start + 9, "]]>");
                continue;
            }

            if (start + 1 >= source.Length) {
                break;
            }

            if (source[start + 1] is '!' or '?') {
                position = SkipTagEnd(source, start + 2);
                continue;
            }

            var closing = source[start + 1] == '/';
            var nameStart = start + (closing ? 2 : 1);
            while (nameStart < source.Length && char.IsWhiteSpace(source[nameStart])) {
                nameStart++;
            }

            var nameEnd = nameStart;
            while (nameEnd < source.Length && IsNameCharacter(source[nameEnd])) {
                nameEnd++;
            }

            var end = SkipTagEnd(source, nameEnd);
            if (nameEnd == nameStart) {
                position = Math.Max(end, start + 1);
                continue;
            }

            tokens.Add(new MarkupTagToken(
                closing ? MarkupTagTokenKind.Closing : MarkupTagTokenKind.Opening,
                source.Substring(nameStart, nameEnd - nameStart),
                start,
                end,
                nameStart,
                nameEnd,
                !closing && IsSelfClosing(source, nameEnd, end)));
            position = Math.Max(end, start + 1);
        }

        return tokens;
    }

    private static bool IsSelfClosing(string source, int start, int end) {
        var position = Math.Min(end - 1, source.Length - 1);
        while (position >= start && char.IsWhiteSpace(source[position])) {
            position--;
        }

        return position >= start && source[position] == '/';
    }

    private static int SkipDelimited(string source, int start, string terminator) {
        var end = source.IndexOf(terminator, start, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + terminator.Length;
    }

    /// <summary>Skips to just past the closing <c>&gt;</c>, ignoring those inside quotes.</summary>
    private static int SkipTagEnd(string source, int start) {
        var quote = '\0';
        for (var index = start; index < source.Length; index++) {
            var current = source[index];
            if (quote != '\0') {
                if (current == quote) {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'') {
                quote = current;
                continue;
            }

            if (current == '>') {
                return index + 1;
            }
        }

        return source.Length;
    }

    private static bool StartsWith(string source, int offset, string value) {
        return offset + value.Length <= source.Length &&
               source.AsSpan(offset, value.Length).SequenceEqual(value);
    }

    private static bool IsNameCharacter(char value) {
        return char.IsLetterOrDigit(value) ||
               value is '_' or '-' or '.' or ':';
    }
}

/// <summary>One raw tag. The index fills in the nesting around it.</summary>
internal sealed class MarkupTagToken {
    public MarkupTagToken(
        MarkupTagTokenKind kind,
        string name,
        int tagStart,
        int tagEnd,
        int nameStart,
        int nameEnd,
        bool isSelfClosing) {
        Kind = kind;
        Name = name;
        TagStart = tagStart;
        TagEnd = tagEnd;
        NameStart = nameStart;
        NameEnd = nameEnd;
        IsSelfClosing = isSelfClosing;
    }

    public MarkupTagTokenKind Kind { get; }

    public string Name { get; }

    public int TagStart { get; }

    public int TagEnd { get; }

    public int NameStart { get; }

    public int NameEnd { get; }

    public bool IsSelfClosing { get; }

    /// <summary>The opening tag this one nests in, or <c>-1</c> for a root element.</summary>
    public int ParentIndex { get; set; } = -1;

    /// <summary>The other half of the pair, or <c>-1</c> while unmatched.</summary>
    public int MatchIndex { get; set; } = -1;
}

internal enum MarkupTagTokenKind {
    Opening,
    Closing,
}
