using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal sealed partial class MarkupControlTokenizer {
    private readonly Action<MarkupDiagnostic> _report;
    private readonly XmlSourceMap _sourceMap;
    private readonly MarkupCodeMask _codeMask;

    public MarkupControlTokenizer(
        XmlSourceMap sourceMap,
        MarkupCodeMask codeMask,
        Action<MarkupDiagnostic> report) {
        _sourceMap = sourceMap;
        _codeMask = codeMask;
        _report = report;
    }

    public IReadOnlyList<MarkupTemplateToken> Tokenize(XElement parent) {
        var tokens = new List<MarkupTemplateToken>();
        var depth = 0;

        foreach (var child in parent.Nodes()) {
            switch (child) {
                case XText text:
                    var offset = _sourceMap.Offset(text);
                    TokenizeText(
                        _codeMask.Restore(text.Value, offset),
                        offset,
                        tokens,
                        ref depth);
                    break;

                case XElement element:
                    tokens.Add(MarkupTemplateToken.FromElement(
                        element,
                        _sourceMap.Offset(element)));
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

            if (IsInterpolationStart(text, index)) {
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

    private static bool IsInterpolationStart(string text, int index) {
        return text[index] == '{' &&
               index + 1 < text.Length &&
               text[index + 1] == '{';
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
