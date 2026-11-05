using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    private static IReadOnlyList<CssSelectorPart> SplitSelectors(string source, int offset) {
        var selectors = new List<CssSelectorPart>();
        var start = 0;
        for (var index = 0; index <= source.Length; index++) {
            if (index != source.Length && source[index] != ',') {
                continue;
            }

            var raw = source.Substring(start, index - start);
            var leading = 0;
            while (leading < raw.Length && char.IsWhiteSpace(raw[leading])) {
                leading++;
            }

            var selector = raw.Trim();
            if (selector.Length != 0) {
                selectors.Add(new CssSelectorPart(selector, offset + start + leading));
            }

            start = index + 1;
        }

        return selectors;
    }

    private static void SkipToBlockEnd(string source, ref int index) {
        while (index < source.Length && source[index] != '{' && source[index] != ';') {
            index++;
        }

        if (index >= source.Length || source[index] == ';') {
            return;
        }

        var depth = 0;
        while (index < source.Length) {
            if (StartsComment(source, index)) {
                SkipComment(source, ref index);
                continue;
            }

            if (source[index] == '{') {
                depth++;
            } else if (source[index] == '}' && --depth == 0) {
                index++;
                return;
            }

            index++;
        }
    }

    private static void SkipToStatementEnd(string source, ref int index) {
        var depth = 0;
        while (index < source.Length) {
            if (StartsComment(source, index)) {
                SkipComment(source, ref index);
                continue;
            }

            if (source[index] == '{') {
                depth++;
            } else if (source[index] == '}') {
                if (depth == 0) {
                    return;
                }

                depth--;
            } else if (source[index] == ';' && depth == 0) {
                index++;
                return;
            }

            index++;
        }
    }

    private static void SkipTrivia(string source, ref int index) {
        while (index < source.Length) {
            if (char.IsWhiteSpace(source[index])) {
                index++;
                continue;
            }

            if (StartsComment(source, index)) {
                SkipComment(source, ref index);
                continue;
            }

            break;
        }
    }

    private static void SkipWhitespace(string source, ref int index) {
        while (index < source.Length && char.IsWhiteSpace(source[index])) {
            index++;
        }
    }

    private static bool StartsComment(string source, int index) {
        return index + 1 < source.Length &&
               source[index] == '/' &&
               source[index + 1] == '*';
    }

    private static void SkipComment(string source, ref int index) {
        var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
        index = end < 0 ? source.Length : end + 2;
    }

    private static string StripComments(string source) {
        var index = 0;
        var result = new System.Text.StringBuilder(source.Length);
        while (index < source.Length) {
            if (StartsComment(source, index)) {
                SkipComment(source, ref index);
                continue;
            }

            result.Append(source[index]);
            index++;
        }

        return result.ToString();
    }

    private sealed class CssSelectorPart {
        public CssSelectorPart(string value, int offset) {
            Value = value;
            Offset = offset;
        }

        public string Value { get; }

        public int Offset { get; }
    }
}
