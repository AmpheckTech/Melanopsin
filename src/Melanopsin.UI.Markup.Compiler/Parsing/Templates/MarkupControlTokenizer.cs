using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal sealed class MarkupControlTokenizer {
    private readonly Action<MarkupDiagnostic> _report;
    private readonly XmlSourceMap _sourceMap;

    public MarkupControlTokenizer(
        XmlSourceMap sourceMap,
        Action<MarkupDiagnostic> report) {
        _sourceMap = sourceMap;
        _report = report;
    }

    public IReadOnlyList<MarkupTemplateToken> Tokenize(XElement parent) {
        var tokens = new List<MarkupTemplateToken>();
        var depth = 0;

        foreach (var child in parent.Nodes()) {
            switch (child) {
                case XText text:
                    TokenizeText(text.Value, _sourceMap.Offset(text), tokens, ref depth);
                    break;

                case XElement element:
                    tokens.Add(MarkupTemplateToken.FromElement(element, _sourceMap.Offset(element)));
                    break;
            }
        }

        return tokens;
    }

    private void TokenizeText(
        string text,
        int baseOffset,
        List<MarkupTemplateToken> tokens,
        ref int depth) {
        var segmentStart = 0;
        var index = 0;

        while (index < text.Length) {
            if (StartsWith(text, index, "@@")) {
                AddTextSegment(tokens, text, segmentStart, index, baseOffset);
                tokens.Add(MarkupTemplateToken.Text("@", baseOffset + index));
                index += 2;
                segmentStart = index;
                continue;
            }

            if (text[index] == '{' &&
                index + 1 < text.Length &&
                text[index + 1] == '{') {
                var interpolationEnd = MarkupInterpolationParser.FindInterpolationEnd(
                    text,
                    index + 2);
                index = interpolationEnd < 0 ? text.Length : interpolationEnd + 2;
                continue;
            }

            if (IsDirectiveStart(text, index)) {
                AddTextSegment(tokens, text, segmentStart, index, baseOffset);
                TryReadDirective(
                    text,
                    baseOffset,
                    tokens,
                    ref index,
                    ref depth);
                segmentStart = index;
                continue;
            }

            if (text[index] == '}' && depth > 0) {
                AddTextSegment(tokens, text, segmentStart, index, baseOffset);
                tokens.Add(MarkupTemplateToken.EndBlock(baseOffset + index));
                depth--;
                index++;
                segmentStart = index;
                continue;
            }

            index++;
        }

        AddTextSegment(tokens, text, segmentStart, text.Length, baseOffset);
    }

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

        return false;
    }

    private static bool IsDirectiveStart(string text, int index) {
        return StartsDirective(text, index, "@if") ||
               StartsDirective(text, index, "@for") ||
               StartsDirective(text, index, "@else") ||
               StartsDirective(text, index, "@empty") ||
               StartsDirective(text, index, "@switch") ||
               StartsDirective(text, index, "@case") ||
               StartsDirective(text, index, "@default");
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
            "if");
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "if")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.If(condition, baseOffset + offset));
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
            "for");
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "for")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.For(header, baseOffset + offset));
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
                "else if");
            if (!ConsumeOpenBrace(text, baseOffset, ref index, "else if")) {
                return true;
            }

            tokens.Add(MarkupTemplateToken.ElseIf(condition, baseOffset + offset));
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
            "switch");
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "switch")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Switch(expression, baseOffset + offset));
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
            "case");
        if (!ConsumeOpenBrace(text, baseOffset, ref index, "case")) {
            return true;
        }

        tokens.Add(MarkupTemplateToken.Case(pattern, baseOffset + offset));
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

    private string ReadDirectiveCondition(
        string text,
        int baseOffset,
        ref int index,
        string directive) {
        SkipWhitespace(text, ref index);
        if (index >= text.Length || text[index] != '(') {
            Report(
                $"@{directive} requires a parenthesized condition.",
                baseOffset + index);
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
                    var value = text.Substring(start, index - start).Trim();
                    index++;
                    if (value.Length == 0) {
                        Report($"@{directive} condition cannot be empty.", baseOffset + start);
                    }

                    return value;
                }
            }

            index++;
        }

        Report($"Unterminated @{directive} condition.", baseOffset + start - 1);
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

    private void AddTextSegment(
        List<MarkupTemplateToken> tokens,
        string text,
        int start,
        int end,
        int baseOffset) {
        if (end <= start) {
            return;
        }

        var value = text.Substring(start, end - start);
        if (!string.IsNullOrWhiteSpace(value)) {
            tokens.Add(MarkupTemplateToken.Text(value, baseOffset + start));
        }
    }

    private void Report(string message, int offset) {
        _report(new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidSyntax,
            message,
            offset,
            1));
    }

    private static bool StartsDirective(string text, int index, string directive) {
        return StartsWith(text, index, directive) &&
               (index + directive.Length >= text.Length ||
                !IsIdentifierCharacter(text[index + directive.Length]));
    }

    private static bool StartsKeyword(string text, int index, string keyword) {
        return StartsWith(text, index, keyword) &&
               (index + keyword.Length >= text.Length ||
                !IsIdentifierCharacter(text[index + keyword.Length]));
    }

    private static bool StartsWith(string text, int index, string value) {
        return index + value.Length <= text.Length &&
               string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static bool IsIdentifierCharacter(char value) {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static void SkipWhitespace(string text, ref int index) {
        while (index < text.Length && char.IsWhiteSpace(text[index])) {
            index++;
        }
    }
}
