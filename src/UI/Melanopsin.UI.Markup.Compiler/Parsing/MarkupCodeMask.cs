using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

/// <summary>
/// Blanks the C# embedded in a markup document before it reaches the XML reader and puts it back
/// while tokenizing. The character count stays identical, so line, column and offset information
/// survives the round trip.
/// </summary>
internal sealed class MarkupCodeMask {
    private readonly string _source;
    private readonly Span[] _spans;

    private MarkupCodeMask(string source, string encoded, Span[] spans) {
        _source = source;
        _spans = spans;
        Encoded = encoded;
    }

    /// <summary>Gets the source with every embedded C# span replaced by blank characters.</summary>
    public string Encoded { get; }

    public static MarkupCodeMask Apply(string source) {
        // Line endings are normalized by the XML reader, which would shift every offset inside a
        // multi-line node. Documents using them keep the older, unprocessed behavior.
        if (source.IndexOf('@') < 0 || source.IndexOf('\r') >= 0) {
            return new MarkupCodeMask(source, source, []);
        }

        var encoded = source.ToCharArray();
        var spans = new List<Span>();
        var index = 0;
        var inTag = false;
        while (index < source.Length) {
            var current = source[index];
            if (inTag) {
                if (current is '"' or '\'') {
                    index = SkipAttributeValue(source, index);
                    continue;
                }

                if (current == '>') {
                    inTag = false;
                }

                index++;
                continue;
            }

            if (current == '<') {
                index = SkipMarkupConstruct(source, index, ref inTag);
                continue;
            }

            if (current == '@') {
                if (index + 1 < source.Length && source[index + 1] == '@') {
                    index += 2;
                    continue;
                }

                if (TryMaskDirective(source, index, out var span, out var next)) {
                    Blank(encoded, span);
                    spans.Add(span);
                    index = next;
                    continue;
                }
            }

            index++;
        }

        return new MarkupCodeMask(source, new string(encoded), [.. spans]);
    }

    /// <summary>
    /// Returns the tokenizer's view of a text node: the node value from the encoded document with
    /// every masked span restored from the original source.
    /// </summary>
    public string Restore(string text, int baseOffset) {
        if (_spans.Length == 0) {
            return text;
        }

        var end = baseOffset + text.Length;
        char[]? restored = null;
        foreach (var span in _spans) {
            if (span.Start >= end) {
                break;
            }

            var start = Math.Max(span.Start, baseOffset);
            var stop = Math.Min(span.End, end);
            if (stop <= start) {
                continue;
            }

            restored ??= text.ToCharArray();
            for (var position = start; position < stop; position++) {
                restored[position - baseOffset] = _source[position];
            }
        }

        return restored is null ? text : new string(restored);
    }

    private static bool TryMaskDirective(
        string source,
        int index,
        out Span span,
        out int next) {
        span = default;
        next = index;
        var keyword = ReadKeyword(source, index);
        switch (keyword) {
            case "code":
                return TryMaskCode(source, index + "@code".Length, out span, out next);
            case "if":
            case "for":
            case "switch":
            case "case":
                return TryMaskCondition(source, index + keyword.Length + 1, out span, out next);
            case "else":
                var probe = SkipWhitespace(source, index + "@else".Length);
                if (!StartsWithKeyword(source, probe, "if")) {
                    return false;
                }

                return TryMaskCondition(source, probe + "if".Length, out span, out next);
            default:
                return false;
        }
    }

    private static bool TryMaskCode(
        string source,
        int index,
        out Span span,
        out int next) {
        span = default;
        next = index;
        var brace = SkipWhitespace(source, index);
        if (brace >= source.Length || source[brace] != '{') {
            return false;
        }

        var bodyStart = brace + 1;
        var bodyEnd = CSharpBlockScanner.FindBlockEnd(source, bodyStart);
        span = new Span(bodyStart, bodyEnd);
        next = bodyEnd < source.Length ? bodyEnd + 1 : source.Length;
        return true;
    }

    private static bool TryMaskCondition(
        string source,
        int index,
        out Span span,
        out int next) {
        span = default;
        next = index;
        var open = SkipWhitespace(source, index);
        if (open >= source.Length || source[open] != '(') {
            return false;
        }

        var close = CSharpBlockScanner.FindGroupEnd(source, open + 1);
        if (close < 0) {
            return false;
        }

        span = new Span(open + 1, close);
        next = close + 1;
        return true;
    }

    private static int SkipMarkupConstruct(string source, int index, ref bool inTag) {
        if (StartsWith(source, index, "<!--")) {
            return SkipTo(source, index + 4, "-->");
        }

        if (StartsWith(source, index, "<![CDATA[")) {
            return SkipTo(source, index + 9, "]]>");
        }

        if (StartsWith(source, index, "<?")) {
            return SkipTo(source, index + 2, "?>");
        }

        if (StartsWith(source, index, "<!")) {
            return SkipTo(source, index + 2, ">");
        }

        inTag = true;
        return index + 1;
    }

    private static int SkipAttributeValue(string source, int index) {
        var quote = source[index];
        index++;
        while (index < source.Length) {
            if (source[index] == quote) {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static int SkipTo(string source, int index, string terminator) {
        var end = source.IndexOf(terminator, index, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + terminator.Length;
    }

    private static string ReadKeyword(string source, int index) {
        var start = index + 1;
        var end = start;
        while (end < source.Length && IsIdentifierCharacter(source[end])) {
            end++;
        }

        return source.Substring(start, end - start);
    }

    private static bool StartsWithKeyword(string source, int index, string keyword) {
        return StartsWith(source, index, keyword) &&
               (index + keyword.Length >= source.Length ||
                !IsIdentifierCharacter(source[index + keyword.Length]));
    }

    private static bool StartsWith(string source, int index, string value) {
        return index + value.Length <= source.Length &&
               string.CompareOrdinal(source, index, value, 0, value.Length) == 0;
    }

    private static int SkipWhitespace(string source, int index) {
        while (index < source.Length && char.IsWhiteSpace(source[index])) {
            index++;
        }

        return index;
    }

    private static bool IsIdentifierCharacter(char value) {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static void Blank(char[] encoded, Span span) {
        for (var index = span.Start; index < span.End; index++) {
            if (encoded[index] != '\n') {
                encoded[index] = ' ';
            }
        }
    }

    private readonly record struct Span(int Start, int End);
}
