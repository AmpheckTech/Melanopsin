using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class MarkupLanguageService {
    private static IEnumerable<MarkupElementInfo> Flatten(MarkupElementInfo element) {
        yield return element;
        foreach (var child in element.Children) {
            foreach (var descendant in Flatten(child)) {
                yield return descendant;
            }
        }
    }

    private static bool Contains(int offset, int length, int position) {
        return position >= offset && position <= offset + length;
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

    private static bool IsAsciiLetter(char value) {
        return value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }

    private static (int Start, int End) IdentifierAt(string source, int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        if (position == source.Length ||
            !IsIdentifierPart(source[position])) {
            if (position == 0 || !IsIdentifierPart(source[position - 1])) {
                return (position, position);
            }

            position--;
        }

        var start = position;
        while (start > 0 && IsIdentifierPart(source[start - 1])) {
            start--;
        }

        var end = position + 1;
        while (end < source.Length && IsIdentifierPart(source[end])) {
            end++;
        }

        return (start, end);
    }

    private static int FindInterpolationStart(string source, int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        var open = source.LastIndexOf("{{", position, StringComparison.Ordinal);
        if (open < 0) {
            return -1;
        }

        var close = source.LastIndexOf("}}", position, StringComparison.Ordinal);
        return open > close ? open : -1;
    }

    private static bool TryFindOpenTag(
        string source,
        int offset,
        out StartTagSpan tag) {
        var cursor = Math.Min(Math.Max(offset, 0), source.Length);
        var start = source.LastIndexOf('<', Math.Max(0, cursor - 1));
        if (start < 0 || start + 1 >= source.Length) {
            tag = default;
            return false;
        }

        var next = source[start + 1];
        if (next is '/' or '!' or '?') {
            tag = default;
            return false;
        }

        var quote = '\0';
        var end = source.Length;
        for (var index = start + 1; index < source.Length; index++) {
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
                end = index;
                break;
            }
        }

        if (cursor > end) {
            tag = default;
            return false;
        }

        var nameStart = start + 1;
        while (nameStart < source.Length && char.IsWhiteSpace(source[nameStart])) {
            nameStart++;
        }

        var nameEnd = nameStart;
        while (nameEnd < source.Length &&
               (IsIdentifierPart(source[nameEnd]) || source[nameEnd] is ':' or '.')) {
            nameEnd++;
        }

        tag = new StartTagSpan(
            start,
            end,
            nameStart,
            nameEnd,
            ReadAttributes(source, nameEnd, end));
        return true;
    }

    private static IReadOnlyList<AttributeSpan> ReadAttributes(
        string source,
        int start,
        int end) {
        var attributes = new List<AttributeSpan>();
        var cursor = start;
        while (cursor < end) {
            while (cursor < end && char.IsWhiteSpace(source[cursor])) {
                cursor++;
            }

            if (cursor >= end || source[cursor] == '/') {
                break;
            }

            var nameStart = cursor;
            while (cursor < end &&
                   !char.IsWhiteSpace(source[cursor]) &&
                   source[cursor] != '=') {
                cursor++;
            }

            var nameEnd = cursor;
            while (cursor < end && char.IsWhiteSpace(source[cursor])) {
                cursor++;
            }

            if (cursor >= end || source[cursor] != '=') {
                attributes.Add(new AttributeSpan(nameStart, nameEnd, -1, -1));
                continue;
            }

            cursor++;
            while (cursor < end && char.IsWhiteSpace(source[cursor])) {
                cursor++;
            }

            if (cursor >= end || source[cursor] is not ('"' or '\'')) {
                var valueStart = cursor;
                while (cursor < end && !char.IsWhiteSpace(source[cursor])) {
                    cursor++;
                }

                attributes.Add(new AttributeSpan(nameStart, nameEnd, valueStart, cursor));
                continue;
            }

            var quote = source[cursor++];
            var quotedValueStart = cursor;
            while (cursor < end && source[cursor] != quote) {
                cursor++;
            }

            attributes.Add(new AttributeSpan(
                nameStart,
                nameEnd,
                quotedValueStart,
                cursor));
            if (cursor < end) {
                cursor++;
            }
        }

        return attributes;
    }

    private static string? FindTagName(string source, int offset) {
        return TryFindOpenTag(source, offset, out var tag) && tag.NameEnd > tag.NameStart
            ? source.Substring(tag.NameStart, tag.NameEnd - tag.NameStart)
            : null;
    }

    private static MarkupElementInfo? FindInnermostElement(
        IReadOnlyList<MarkupElementInfo> elements,
        string source,
        int offset) {
        MarkupElementInfo? best = null;
        foreach (var element in elements.SelectMany(Flatten)) {
            if (!TryFindOpenTag(source, element.Offset, out var tag) ||
                !Contains(element.Offset, tag.End - element.Offset, offset)) {
                continue;
            }

            if (best is null || element.Offset >= best.Offset) {
                best = element;
            }
        }

        return best;
    }

    private readonly record struct StartTagSpan(
        int Start,
        int End,
        int NameStart,
        int NameEnd,
        IReadOnlyList<AttributeSpan> Attributes);

    private readonly record struct AttributeSpan(
        int NameStart,
        int NameEnd,
        int ValueStart,
        int ValueEnd);
}
