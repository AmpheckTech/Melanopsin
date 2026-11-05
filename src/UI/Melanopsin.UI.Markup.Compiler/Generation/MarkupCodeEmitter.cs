// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

/// <summary>
/// One <c>@code</c> block, split into the leading <c>using</c> directives that belong to the
/// generated file and the remaining class body.
/// </summary>
internal sealed record MarkupCodeUnit(
    string Usings,
    string Body,
    int StartLine,
    int BodyLine);

internal static class MarkupCodeEmitter {
    public static IReadOnlyList<MarkupCodeUnit> Read(MarkupDocument document) {
        if (document.CodeBlocks.Count == 0) {
            return Array.Empty<MarkupCodeUnit>();
        }

        var units = new List<MarkupCodeUnit>(document.CodeBlocks.Count);
        foreach (var block in document.CodeBlocks) {
            var usings = ReadUsings(block.Text, out var body);
            units.Add(new MarkupCodeUnit(
                usings,
                body,
                LineAt(document.Source, block.Offset),
                LineAt(document.Source, block.Offset) + NewLines(usings)));
        }

        return units;
    }

    /// <summary>Writes the hoisted <c>using</c> directives at file scope.</summary>
    public static void WriteUsings(StringBuilder output, IReadOnlyList<MarkupCodeUnit> units, string path) {
        foreach (var unit in units) {
            if (unit.Usings.Length == 0) {
                continue;
            }

            AppendLineDirective(output, unit.StartLine, path);
            output.Append(unit.Usings);
            if (!unit.Usings.EndsWith("\n", StringComparison.Ordinal)) {
                output.AppendLine();
            }

            output.AppendLine("#line default");
        }
    }

    /// <summary>Writes the <c>@code</c> bodies into the component class.</summary>
    public static void WriteBodies(StringBuilder output, IReadOnlyList<MarkupCodeUnit> units, string path) {
        foreach (var unit in units) {
            if (unit.Body.Trim().Length == 0) {
                continue;
            }

            output.AppendLine();
            AppendLineDirective(output, unit.BodyLine, path);
            output.Append(unit.Body);
            if (!unit.Body.EndsWith("\n", StringComparison.Ordinal)) {
                output.AppendLine();
            }

            output.AppendLine("#line default");
        }
    }

    /// <summary>Writes the attribute marking a class as the one a markup document materializes.</summary>
    public static void WriteMarker(StringBuilder output) {
        output.Append("[global::Melanopsin.UI.Markup.MarkupComponent]").AppendLine();
    }

    /// <summary>Writes the class declaration that materializes a markup-only component.</summary>
    public static void WriteDeclaration(StringBuilder output, string className) {
        output.Append("public sealed partial class ")
            .Append(className)
            .AppendLine(" : global::Melanopsin.UI.Component {");
    }

    /// <summary>
    /// Points the text that follows at a markup line, so a location in generated code - the class
    /// declaration of a markup-only component, most of all - navigates into the <c>.mxml</c>.
    /// </summary>
    public static void WriteLineDirective(StringBuilder output, int line, string path) {
        AppendLineDirective(output, line, path);
    }

    /// <summary>Ends the effect of <see cref="WriteLineDirective"/>.</summary>
    public static void WriteLineDefault(StringBuilder output) {
        output.AppendLine("#line default");
    }

    private static void AppendLineDirective(StringBuilder output, int line, string path) {
        output.Append("#line ").Append(line).Append(" \"")
            .Append(path.Replace("\\", "\\\\"))
            .AppendLine("\"");
    }

    private static int NewLines(string text) {
        var count = 0;
        foreach (var character in text) {
            if (character == '\n') {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Splits a code block into leading <c>using</c> directives and the class body. Only directives
    /// at the very start of the block are hoisted; everything else stays in the class.
    /// </summary>
    private static string ReadUsings(string text, out string body) {
        var index = 0;
        var prefixEnd = 0;
        while (index < text.Length) {
            var probe = index;
            while (probe < text.Length && char.IsWhiteSpace(text[probe])) {
                probe++;
            }

            if (!StartsWithUsing(text, probe)) {
                break;
            }

            var terminator = text.IndexOf(';', probe);
            if (terminator < 0) {
                break;
            }

            var lineEnd = text.IndexOf('\n', terminator);
            index = lineEnd < 0 ? text.Length : lineEnd + 1;
            prefixEnd = index;
        }

        body = text.Substring(prefixEnd);
        return text.Substring(0, prefixEnd);
    }

    private static bool StartsWithUsing(string text, int index) {
        const string Keyword = "using ";
        if (index + Keyword.Length > text.Length ||
            string.CompareOrdinal(text, index, Keyword, 0, Keyword.Length) != 0) {
            return false;
        }

        // "using (" opens a using statement, not a directive; it stays in the class body.
        var next = index + Keyword.Length;
        while (next < text.Length && char.IsWhiteSpace(text[next])) {
            next++;
        }

        return next >= text.Length || text[next] != '(';
    }

    /// <summary>The one-based line an offset falls on.</summary>
    public static int LineAt(string source, int offset) {
        var line = 1;
        for (var index = 0; index < offset && index < source.Length; index++) {
            if (source[index] == '\n') {
                line++;
            }
        }

        return line;
    }
}
