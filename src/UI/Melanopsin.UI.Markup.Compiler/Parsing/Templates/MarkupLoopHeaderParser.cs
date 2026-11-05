// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

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
        var loop = Trimmed(parts[0], out var loopStart);
        if (loop.StartsWith("var ", StringComparison.Ordinal)) {
            loopStart += 4 + LeadingWhitespace(loop, 4);
            loop = loop.Substring(4).TrimStart();
        }

        var separator = FindLoopSeparator(loop);
        if (separator.Index < 0) {
            Report(
                "Use '@for (item of items)' or '@for (var item in items)'.",
                offset);
            return new MarkupLoopHeader(
                "$item",
                -1,
                "global::System.Array.Empty<object>()",
                -1,
                null);
        }

        var variable = loop.Substring(0, separator.Index).Trim();
        var iterableStart = separator.Index + separator.Length;
        var iterable = loop.Substring(iterableStart).TrimStart();
        iterableStart += LeadingWhitespace(loop, separator.Index + separator.Length);
        string? track = null;
        var trackOffset = -1;

        for (var index = 1; index < parts.Count; index++) {
            var clause = Trimmed(parts[index], out var clauseStart);
            if (clause.StartsWith("track ", StringComparison.Ordinal)) {
                var trackStart = "track ".Length + LeadingWhitespace(clause, "track ".Length);
                track = clause.Substring("track ".Length).TrimStart();
                trackOffset = Locate(offset, clauseStart + trackStart);
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

        return new MarkupLoopHeader(
            variable,
            Locate(offset, loopStart + iterableStart),
            iterable,
            trackOffset,
            track);
    }

    /// <summary>A clause without its surrounding whitespace, and where it starts in the header.</summary>
    private static string Trimmed(Part part, out int start) {
        start = part.Start + LeadingWhitespace(part.Text, 0);
        return part.Text.Trim();
    }

    /// <summary>The count of whitespace characters a value starts with, up to <paramref name="limit"/>.</summary>
    private static int LeadingWhitespace(string value, int from) {
        var index = from;
        while (index < value.Length && char.IsWhiteSpace(value[index])) {
            index++;
        }

        return index - from;
    }

    /// <summary>An offset inside the header turned into one inside the document, or -1.</summary>
    private static int Locate(int headerOffset, int relative) {
        return headerOffset < 0 ? -1 : headerOffset + relative;
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

    private readonly record struct Part(string Text, int Start);

    private static IReadOnlyList<Part> SplitTopLevel(string value, char separator) {
        var parts = new List<Part>();
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
                parts.Add(new Part(value.Substring(start, index - start), start));
                start = index + 1;
            }
        }

        parts.Add(new Part(value.Substring(start), start));
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
