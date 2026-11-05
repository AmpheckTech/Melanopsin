// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

/// <summary>
/// Finds the end of a C# span embedded in markup text. Literals and comments are skipped so that
/// braces and parentheses inside them do not close the span early.
/// </summary>
internal static class CSharpBlockScanner {
    /// <summary>
    /// Returns the index of the '}' closing the block whose body starts at
    /// <paramref name="start"/>, or the length of <paramref name="text"/> when the block is not
    /// terminated.
    /// </summary>
    public static int FindBlockEnd(string text, int start) {
        var depth = 1;
        var index = start;
        while (index < text.Length) {
            var current = text[index];
            if (current == '/' && index + 1 < text.Length) {
                if (text[index + 1] == '/') {
                    index = SkipLineComment(text, index);
                    continue;
                }

                if (text[index + 1] == '*') {
                    index = SkipBlockComment(text, index);
                    continue;
                }
            }

            if (current is '"' or '\'') {
                index = SkipLiteral(text, index);
                continue;
            }

            if (current == '{') {
                depth++;
            } else if (current == '}') {
                depth--;
                if (depth == 0) {
                    return index;
                }
            }

            index++;
        }

        return text.Length;
    }

    /// <summary>
    /// Returns the index of the ')' closing the group that starts at <paramref name="start"/>, or
    /// -1 when the group is not terminated.
    /// </summary>
    public static int FindGroupEnd(string text, int start) {
        var depth = 1;
        var index = start;
        while (index < text.Length) {
            var current = text[index];
            if (current is '"' or '\'') {
                index = SkipLiteral(text, index);
                continue;
            }

            if (current == '(') {
                depth++;
            } else if (current == ')') {
                depth--;
                if (depth == 0) {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    private static int SkipLineComment(string text, int index) {
        while (index < text.Length && text[index] != '\n') {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string text, int index) {
        index += 2;
        while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/')) {
            index++;
        }

        return Math.Min(text.Length, index + 2);
    }

    private static int SkipLiteral(string text, int index) {
        var quote = text[index];
        if (quote == '"' && IsVerbatimPrefix(text, index)) {
            index++;
            while (index < text.Length) {
                if (text[index] == '"') {
                    if (index + 1 < text.Length && text[index + 1] == '"') {
                        index += 2;
                        continue;
                    }

                    return index + 1;
                }

                index++;
            }

            return text.Length;
        }

        index++;
        while (index < text.Length) {
            if (text[index] == '\\') {
                index += 2;
                continue;
            }

            if (text[index] == quote) {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }

    private static bool IsVerbatimPrefix(string text, int index) {
        if (index == 0) {
            return false;
        }

        if (text[index - 1] == '@') {
            return true;
        }

        return text[index - 1] == '$' && index > 1 && text[index - 2] == '@';
    }
}
