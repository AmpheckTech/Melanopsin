// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal sealed partial class MarkupControlTokenizer {
    private bool TryReadDirective(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        if (StartsDirective(text, index, "@if")) {
            return TryReadIf(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@for")) {
            return TryReadFor(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@else")) {
            return TryReadElse(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@empty")) {
            return TryReadEmpty(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@switch")) {
            return TryReadSwitch(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@case")) {
            return TryReadCase(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@default")) {
            return TryReadDefault(text, baseOffset, tokens, ref index, ref depth);
        }

        if (StartsDirective(text, index, "@code")) {
            return TryReadCode(text, baseOffset, tokens, ref index);
        }

        return false;
    }

    private static bool IsDirectiveStart(string text, int index) {
        return StartsDirective(text, index, "@if") ||
               StartsDirective(text, index, "@for") ||
               StartsDirective(text, index, "@else") ||
               StartsDirective(text, index, "@empty") ||
               StartsDirective(text, index, "@switch") ||
               StartsDirective(text, index, "@case") ||
               StartsDirective(text, index, "@default") ||
               StartsDirective(text, index, "@code");
    }

    private bool TryReadIf(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@if".Length;
        var condition = ReadDirectiveCondition(
            text,
            baseOffset,
            ref index,
            "if",
            out var conditionOffset);
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "if")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.If(condition, baseOffset + offset, conditionOffset));
        depth++;
        return true;
    }

    private bool TryReadFor(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@for".Length;
        var header = ReadDirectiveCondition(
            text,
            baseOffset,
            ref index,
            "for",
            out var headerOffset);
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "for")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.For(header, baseOffset + offset, headerOffset));
        depth++;
        return true;
    }

    private bool TryReadElse(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@else".Length;
        SkipWhitespace(text, ref index);

        if (StartsKeyword(text, index, "if")) {
            index += "if".Length;
            var condition = ReadDirectiveCondition(
                text,
                baseOffset,
                ref index,
                "else if",
                out var conditionOffset);
            if (!ConsumeOpenBrace(text, baseOffset, ref index, "else if")) {
                return true;
            }

            tokens.Add(MarkupTemplateToken.ElseIf(
                condition,
                baseOffset + offset,
                conditionOffset));
            depth++;
            return true;
        }

        if (!ConsumeOpenBrace(text, baseOffset, ref index, "else")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Else(baseOffset + offset));
        depth++;
        return true;
    }

    private bool TryReadEmpty(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@empty".Length;
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "empty")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Empty(baseOffset + offset));
        depth++;
        return true;
    }

    private bool TryReadSwitch(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@switch".Length;
        var expression = ReadDirectiveCondition(
            text,
            baseOffset,
            ref index,
            "switch",
            out var expressionOffset);
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "switch")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Switch(expression, baseOffset + offset, expressionOffset));
        depth++;
        return true;
    }

    private bool TryReadCase(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@case".Length;
        var pattern = ReadDirectiveCondition(
            text,
            baseOffset,
            ref index,
            "case",
            out var patternOffset);
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "case")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Case(pattern, baseOffset + offset, patternOffset));
        depth++;
        return true;
    }

    private bool TryReadDefault(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index,
        ref int depth) {
        var offset = index;
        index += "@default".Length;
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "default")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Default(baseOffset + offset));
        depth++;
        return true;
    }

    private bool TryReadCode(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int index) {
        index += "@code".Length;
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "code")) {
            return true;
        }

        var start = index;
        var end = CSharpBlockScanner.FindBlockEnd(text, index);
        tokens.Add(MarkupTemplateToken.Code(
            text.Substring(start, Math.Max(0, end - start)),
            baseOffset + start));
        index = end < text.Length ? end + 1 : text.Length;
        if (end >= text.Length) {
            Report("@code block is missing its closing '}'.", baseOffset + Math.Max(0, end - 1));
        }

        return true;
    }

    private string ReadDirectiveCondition(
        string text,
        int baseOffset,
        ref int index,
        string directive,
        out int conditionOffset) {
        SkipWhitespace(text, ref index);
        if (index >= text.Length || text[index] != '(') {
            Report(
                $"@{directive} requires a parenthesized condition.",
                baseOffset + index);
            conditionOffset = -1;
            return string.Empty;
        }

        var start = ++index;
        var depth = 1;
        char? quote = null;
        var escaped = false;

        while (index < text.Length) {
            var current = text[index];
            if (quote is not null) {
                if (escaped) {
                    escaped = false;
                } else if (current == '\\') {
                    escaped = true;
                } else if (current == quote) {
                    quote = null;
                }
            } else if (current is '"' or '\'') {
                quote = current;
            } else if (current == '(') {
                depth++;
            } else if (current == ')') {
                depth--;
                if (depth == 0) {
                    var raw = text.Substring(start, index - start);
                    var value = raw.Trim();
                    index++;
                    if (value.Length == 0) {
                        Report(
                            $"@{directive} condition cannot be empty.",
                            baseOffset + start);
                    }

                    conditionOffset = baseOffset + start + (raw.Length - raw.TrimStart().Length);
                    return value;
                }
            }

            index++;
        }

        Report($"Unterminated @{directive} condition.", baseOffset + start - 1);
        conditionOffset = -1;
        return text.Substring(start).Trim();
    }

    private bool ConsumeOpenBrace(
        string text,
        int baseOffset,
        ref int index,
        string directive) {
        SkipWhitespace(text, ref index);
        if (index < text.Length && text[index] == '{') {
            index++;
            return true;
        }

        Report(
            $"@{directive} block requires an opening '{{'.",
            baseOffset + Math.Min(index, Math.Max(0, text.Length - 1)));
        return false;
    }
}
