using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    public static CssStylesheet Parse(string path, string source) {
        var imports = new List<CssImport>();
        var rules = new List<CssRule>();
        var slots = new List<CssSlot>();
        var slotNames = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<CssDiagnostic>();
        var hasStyleRule = false;
        var index = 0;
        while (true) {
            SkipTrivia(source, ref index);
            if (index >= source.Length) {
                break;
            }

            if (source[index] == '@') {
                var directiveStart = index;
                index++;
                var nameStart = index;
                while (index < source.Length &&
                       (char.IsLetterOrDigit(source[index]) || source[index] is '_' or '-')) {
                    index++;
                }

                var directive = source.Substring(nameStart, index - nameStart);
                SkipWhitespace(source, ref index);
                if (string.Equals(directive, "import", StringComparison.OrdinalIgnoreCase)) {
                    if (hasStyleRule) {
                        diagnostics.Add(new CssDiagnostic(
                            MarkupDiagnosticCodes.InvalidStylesheet,
                            "'@import' directives must appear before style rules.",
                            directiveStart,
                            Math.Max(1, index - directiveStart)));
                    }

                    if (TryParseImport(source, ref index, directiveStart, out var import, diagnostics)) {
                        imports.Add(import);
                    }

                    continue;
                }

                if (string.Equals(directive, "tokens", StringComparison.OrdinalIgnoreCase) &&
                    index < source.Length &&
                    source[index] == '{') {
                    index++;
                    var tokenDeclarations = ParseDeclarations(source, ref index, diagnostics);
                    ParseTokens(tokenDeclarations, slots, slotNames, diagnostics);
                    continue;
                }

                SkipToBlockEnd(source, ref index);
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Unsupported stylesheet directive '@{directive}'. Expected '@import' or '@tokens'.",
                    directiveStart,
                    Math.Max(1, index - directiveStart)));
                continue;
            }

            var selectorStart = index;
            hasStyleRule = true;
            if (source[index] == '}') {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "Unexpected '}'. A style rule is missing its selector.",
                    index,
                    1));
                index++;
                continue;
            }

            while (index < source.Length && source[index] != '{') {
                if (StartsComment(source, index)) {
                    SkipComment(source, ref index);
                    continue;
                }

                if (source[index] == '}') {
                    break;
                }

                index++;
            }

            if (index >= source.Length || source[index] != '{') {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "Style rule is missing '{'.",
                    selectorStart,
                    Math.Max(1, index - selectorStart)));
                if (index < source.Length && source[index] == '}') {
                    index++;
                }

                continue;
            }

            var selectorText = StripComments(
                source.Substring(selectorStart, index - selectorStart)).Trim();
            index++;
            var items = ParseStyleBlock(source, ref index, diagnostics);
            var rule = new CssNestedRule(selectorText, selectorStart, items);
            AddRule(rule, parentSelectors: null, rules, diagnostics);
        }

        return new CssStylesheet(path, source, imports, rules, slots, diagnostics);
    }
}
