using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

internal sealed class MarkupInterpolationParser {
    private readonly Action<MarkupDiagnostic> _report;

    public MarkupInterpolationParser(Action<MarkupDiagnostic> report) {
        _report = report;
    }

    /// <summary>Splits an attribute value into literals, interpolations and resource references.</summary>
    /// <param name="value">The value the XML reader reported.</param>
    /// <param name="offset">Where the value starts in the document, or <c>-1</c> when it cannot be located.</param>
    public IReadOnlyList<MarkupValuePart> ParseAttribute(string value, int offset) {
        var resourceMarker = value.IndexOf("{res", StringComparison.Ordinal);
        if (resourceMarker >= 0) {
            return ParseResourceAttribute(value, offset, resourceMarker);
        }

        var parts = new List<MarkupValuePart>();
        var cursor = 0;

        while (cursor < value.Length) {
            var interpolation = value.IndexOf("{{", cursor, StringComparison.Ordinal);
            if (interpolation < 0) {
                parts.Add(new MarkupValuePart(
                    value.Substring(cursor),
                    MarkupValuePartKind.Literal,
                    MapOffset(offset, cursor)));
                break;
            }

            if (interpolation > cursor) {
                parts.Add(new MarkupValuePart(
                    value.Substring(cursor, interpolation - cursor),
                    MarkupValuePartKind.Literal,
                    MapOffset(offset, cursor)));
            }

            var expressionStart = interpolation + 2;
            var expressionEnd = FindInterpolationEnd(value, expressionStart);
            if (expressionEnd < 0) {
                _report(new MarkupDiagnostic(
                    MarkupDiagnosticCodes.InvalidSyntax,
                    "Unterminated attribute interpolation.",
                    Math.Max(0, offset) + interpolation,
                    2));
                parts.Add(new MarkupValuePart(
                    value.Substring(interpolation),
                    MarkupValuePartKind.Literal,
                    MapOffset(offset, interpolation)));
                break;
            }

            var raw = value.Substring(expressionStart, expressionEnd - expressionStart);
            var expression = raw.Trim();
            if (expression.Length == 0) {
                ReportInvalidExpression(
                    "An interpolation expression cannot be empty.",
                    Math.Max(0, offset) + interpolation);
            }

            parts.Add(new MarkupValuePart(
                expression,
                MarkupValuePartKind.Expression,
                MapOffset(offset, expressionStart + LeadingWhitespace(raw))));
            cursor = expressionEnd + 2;
        }

        if (parts.Count == 0) {
            parts.Add(new MarkupValuePart(string.Empty, MarkupValuePartKind.Literal, MapOffset(offset, 0)));
        }

        return parts;
    }

    private IReadOnlyList<MarkupValuePart> ParseResourceAttribute(
        string value,
        int offset,
        int markerOffset) {
        var trimmed = value.Trim();
        var leadingWhitespace = value.Length - value.TrimStart().Length;
        if (!trimmed.StartsWith("{res ", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal)) {
            ReportInvalidResourceReference(
                "A resource reference must use exactly {res Category.Name}.",
                Math.Max(0, offset) + leadingWhitespace + markerOffset);
            return [new MarkupValuePart(value, MarkupValuePartKind.Literal, offset)];
        }

        var referenceText = trimmed.Substring(
            "{res ".Length,
            trimmed.Length - "{res ".Length - 1).Trim();
        if (!ResourceReference.TryParse(referenceText, out var reference)) {
            ReportInvalidResourceReference(
                "A resource reference must use exactly {res Category.Name}.",
                Math.Max(0, offset) + leadingWhitespace + markerOffset);
            return [new MarkupValuePart(value, MarkupValuePartKind.Literal, offset)];
        }

        return [new MarkupValuePart(reference.Id, MarkupValuePartKind.Resource, MapOffset(offset, leadingWhitespace))];
    }

    private void ReportInvalidResourceReference(string message, int offset) {
        _report(new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidResourceReference,
            message,
            offset,
            "{res".Length));
    }

    public void ParseText(List<MarkupNode> nodes, string text, int offset) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        var cursor = 0;
        while (cursor < text.Length) {
            var interpolation = text.IndexOf("{{", cursor, StringComparison.Ordinal);
            if (interpolation < 0) {
                AddText(nodes, text.Substring(cursor), offset + cursor);
                break;
            }

            AddText(nodes, text.Substring(cursor, interpolation - cursor), offset + cursor);
            var expressionStart = interpolation + 2;
            var expressionEnd = FindInterpolationEnd(text, expressionStart);
            if (expressionEnd < 0) {
                _report(new MarkupDiagnostic(
                    MarkupDiagnosticCodes.InvalidSyntax,
                    "Unterminated text interpolation.",
                    offset + interpolation,
                    2));
                AddText(nodes, text.Substring(interpolation), offset + interpolation);
                break;
            }

            var expression = text.Substring(expressionStart, expressionEnd - expressionStart).Trim();
            if (expression.Length == 0) {
                ReportInvalidExpression(
                    "A text interpolation cannot be empty.",
                    offset + interpolation);
            }

            nodes.Add(new MarkupExpression(
                offset + interpolation,
                offset + expressionStart + LeadingWhitespace(
                    text.Substring(expressionStart, expressionEnd - expressionStart)),
                expression));
            cursor = expressionEnd + 2;
        }
    }

    /// <summary>The offset a substring starts at, or <c>-1</c> for a document-less value.</summary>
    private static int MapOffset(int baseOffset, int relative) {
        return baseOffset < 0 ? -1 : baseOffset + relative;
    }

    private static int LeadingWhitespace(string value) {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index])) {
            index++;
        }

        return index;
    }

    private void ReportInvalidExpression(string message, int offset) {
        _report(new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidExpression,
            message,
            offset,
            2));
    }

    private static void AddText(List<MarkupNode> nodes, string text, int offset) {
        if (!string.IsNullOrWhiteSpace(text)) {
            nodes.Add(new MarkupText(offset, text));
        }
    }

    public static int FindInterpolationEnd(string value, int start) {
        var nesting = 0;
        char? quote = null;
        var escaped = false;

        for (var index = start; index < value.Length; index++) {
            var current = value[index];

            if (quote is not null) {
                if (escaped) {
                    escaped = false;
                } else if (current == '\\') {
                    escaped = true;
                } else if (current == quote) {
                    quote = null;
                }

                continue;
            }

            if (current is '"' or '\'') {
                quote = current;
                continue;
            }

            if (current == '{') {
                nesting++;
                continue;
            }

            if (current == '}') {
                if (nesting == 0 && index + 1 < value.Length && value[index + 1] == '}') {
                    return index;
                }

                if (nesting > 0) {
                    nesting--;
                }
            }
        }

        return -1;
    }
}
