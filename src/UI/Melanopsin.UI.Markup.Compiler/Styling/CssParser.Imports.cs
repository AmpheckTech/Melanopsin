using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    private static bool TryParseImport(
        string source,
        ref int index,
        int directiveOffset,
        out CssImport import,
        List<CssDiagnostic> diagnostics) {
        import = null!;
        if (!TryParseImportPath(
                source,
                ref index,
                out var path,
                out var pathOffset,
                out var pathLength)) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                "'@import' requires a quoted path followed by ';'.",
                directiveOffset,
                Math.Max(1, index - directiveOffset)));
            SkipToStatementEnd(source, ref index);
            return false;
        }

        SkipWhitespace(source, ref index);
        if (index >= source.Length || source[index] != ';') {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                "The '@import' directive must end with ';'. Conditions and media queries are not supported.",
                directiveOffset,
                Math.Max(1, index - directiveOffset)));
            SkipToStatementEnd(source, ref index);
            return false;
        }

        index++;
        import = new CssImport(path, pathOffset, pathLength);
        return true;
    }

    private static bool TryParseImportPath(
        string source,
        ref int index,
        out string path,
        out int pathOffset,
        out int pathLength) {
        path = string.Empty;
        pathOffset = index;
        pathLength = 0;
        if (index >= source.Length) {
            return false;
        }

        if (source[index] is not ('"' or '\'')) {
            return false;
        }

        return TryParseQuotedPath(
            source,
            ref index,
            out path,
            out pathOffset,
            out pathLength);
    }

    private static bool TryParseQuotedPath(
        string source,
        ref int index,
        out string path,
        out int pathOffset,
        out int pathLength) {
        var quote = source[index++];
        pathOffset = index;
        var output = new System.Text.StringBuilder();
        while (index < source.Length && source[index] != quote) {
            if (source[index] == '\\' && index + 1 < source.Length) {
                index++;
            }

            output.Append(source[index++]);
        }

        pathLength = index - pathOffset;
        path = output.ToString();
        if (index >= source.Length || path.Length == 0) {
            return false;
        }

        index++;
        return true;
    }
}
