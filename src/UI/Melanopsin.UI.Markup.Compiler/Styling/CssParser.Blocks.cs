using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    private static IReadOnlyList<CssStyleBlockItem> ParseStyleBlock(
        string source,
        ref int index,
        List<CssDiagnostic> diagnostics) {
        var items = new List<CssStyleBlockItem>();
        while (true) {
            SkipTrivia(source, ref index);
            if (index >= source.Length) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "Style rule is missing '}'.",
                    Math.Max(0, source.Length - 1),
                    1));
                return items;
            }

            if (source[index] == '}') {
                index++;
                return items;
            }

            var itemStart = index;
            var colon = -1;
            var parentheses = 0;
            char quote = '\0';
            while (index < source.Length) {
                if (StartsComment(source, index)) {
                    SkipComment(source, ref index);
                    continue;
                }

                var character = source[index];
                if (quote != '\0') {
                    if (character == '\\' && index + 1 < source.Length) {
                        index += 2;
                        continue;
                    }

                    if (character == quote) {
                        quote = '\0';
                    }

                    index++;
                    continue;
                }

                if (character is '\'' or '"') {
                    quote = character;
                    index++;
                    continue;
                }

                if (character == '(') {
                    parentheses++;
                } else if (character == ')') {
                    parentheses = Math.Max(0, parentheses - 1);
                } else if (character == ':' && parentheses == 0 && colon < 0) {
                    colon = index;
                } else if (parentheses == 0 && character is '{' or ';' or '}') {
                    break;
                }

                index++;
            }

            if (index >= source.Length) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "Style rule is missing '}'.",
                    Math.Max(0, source.Length - 1),
                    1));
                return items;
            }

            if (source[index] == '{') {
                var header = StripComments(
                    source.Substring(itemStart, index - itemStart)).Trim();
                index++;
                var children = ParseStyleBlock(source, ref index, diagnostics);
                if (header.Length == 0) {
                    diagnostics.Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        "Nested style rule has an empty selector.",
                        itemStart,
                        1));
                    continue;
                }

                items.Add(CssStyleBlockItem.FromRule(
                    new CssNestedRule(header, itemStart, children)));
                continue;
            }

            var reachedEnd = source[index] == '}';
            if (colon < 0) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "MCSS declaration is missing ':'.",
                    itemStart,
                    Math.Max(1, index - itemStart)));
            } else {
                var name = StripComments(
                    source.Substring(itemStart, colon - itemStart)).Trim();
                var valueStart = colon + 1;
                var rawValue = source.Substring(valueStart, index - valueStart);
                var leading = 0;
                while (leading < rawValue.Length && char.IsWhiteSpace(rawValue[leading])) {
                    leading++;
                }

                var trailing = rawValue.Length;
                while (trailing > leading && char.IsWhiteSpace(rawValue[trailing - 1])) {
                    trailing--;
                }

                var value = StripComments(rawValue.Substring(leading, trailing - leading)).Trim();
                if (name.Length == 0 || value.Length == 0) {
                    diagnostics.Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        "MCSS declarations require both a property name and a value.",
                        itemStart,
                        Math.Max(1, index - itemStart)));
                } else {
                    if (name.StartsWith("$", StringComparison.Ordinal)) {
                        diagnostics.Add(new CssDiagnostic(
                            MarkupDiagnosticCodes.InvalidStylesheet,
                            "Style tokens must be declared in the @tokens block.",
                            itemStart,
                            Math.Max(1, index - itemStart)));
                    }

                    items.Add(CssStyleBlockItem.FromDeclaration(new CssDeclaration(
                        name,
                        value,
                        itemStart,
                        valueStart + leading,
                        Math.Max(1, trailing - leading))));
                }
            }

            if (reachedEnd) {
                index++;
                return items;
            }

            index++;
        }
    }
}
