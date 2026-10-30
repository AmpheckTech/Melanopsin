using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal sealed class MarkupLoopHeaderParser {
    private readonly Action<MarkupDiagnostic> _report;

    public MarkupLoopHeaderParser(Action<MarkupDiagnostic> report) {
        _report = report;
    }

    public MarkupLoopHeader Parse(string header, int offset) {
        var parts = SplitTopLevel(header, ';');
        var loop = parts[0].Trim();
        if (loop.StartsWith("var ", StringComparison.Ordinal)) {
            loop = loop.Substring(4).TrimStart();
        }

        var separator = FindLoopSeparator(loop);
        if (separator.Index < 0) {
            Report(
                "Use '@for (item of items)' or '@for (var item in items)'.",
                offset);
            return new MarkupLoopHeader(
                "$item",
                "global::System.Array.Empty<object>()",
                null);
        }

        var variable = loop.Substring(0, separator.Index).Trim();
        var iterable = loop.Substring(separator.Index + separator.Length).Trim();
        string? track = null;

        for (var index = 1; index < parts.Count; index++) {
            var clause = parts[index].Trim();
            if (clause.StartsWith("track ", StringComparison.Ordinal)) {
                track = clause.Substring("track ".Length).Trim();
            } else if (clause.Length > 0) {
                Report($"Unsupported @for clause '{clause}'.", offset);
            }
        }

        if (!SyntaxFacts.IsValidIdentifier(variable)) {
            Report($"'{variable}' is not a valid iteration variable.", offset);
        }

        if (iterable.Length == 0) {
            Report("The @for iterable expression cannot be empty.", offset);
        }

        return new MarkupLoopHeader(variable, iterable, track);
    }

    private static (int Index, int Length) FindLoopSeparator(string loop) {
        var of = loop.IndexOf(" of ", StringComparison.Ordinal);
        var @in = loop.IndexOf(" in ", StringComparison.Ordinal);
        if (of < 0) {
            return (@in, @in < 0 ? 0 : 4);
        }

        return @in < 0 || of < @in
            ? (of, 4)
            : (@in, 4);
    }

    private static IReadOnlyList<string> SplitTopLevel(string value, char separator) {
        var parts = new List<string>();
        var start = 0;
        var nesting = 0;
        char? quote = null;
        var escaped = false;

        for (var index = 0; index < value.Length; index++) {
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
            } else if (current is '(' or '[' or '{') {
                nesting++;
            } else if (current is ')' or ']' or '}') {
                nesting--;
            } else if (current == separator && nesting == 0) {
                parts.Add(value.Substring(start, index - start));
                start = index + 1;
            }
        }

        parts.Add(value.Substring(start));
        return parts;
    }

    private void Report(string message, int offset) {
        _report(new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidSyntax,
            message,
            offset,
            1));
    }
}
