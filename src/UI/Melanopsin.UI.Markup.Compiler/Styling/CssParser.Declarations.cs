// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    private static void ParseTokens(
        IReadOnlyList<CssDeclaration> declarations,
        List<CssSlot> slots,
        HashSet<string> slotNames,
        List<CssDiagnostic> diagnostics) {
        foreach (var declaration in declarations) {
            if (!IsTokenName(declaration.Name)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Invalid token name '{declaration.Name}'. Token names contain letters, digits, '_' or '-', and must start with a letter or '_'.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            var separator = FindTokenSeparator(declaration.Value);
            if (separator <= 0 || separator == declaration.Value.Length - 1) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Style token '${declaration.Name}' must declare a type and value, for example 'space: length = 3su;'.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            var typeName = declaration.Value.Substring(0, separator).Trim();
            var value = declaration.Value.Substring(separator + 1).Trim();
            if (typeName.Length == 0 || value.Length == 0) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Style token '${declaration.Name}' must declare a type and value, for example 'space: length = 3su;'.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            if (!slotNames.Add(declaration.Name)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Style token '${declaration.Name}' is declared more than once.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            slots.Add(new CssSlot(
                declaration.Name,
                typeName,
                value,
                declaration.ValueOffset,
                Math.Max(1, value.Length)));
        }
    }

    private static int FindTokenSeparator(string value) {
        var parentheses = 0;
        for (var index = 0; index < value.Length; index++) {
            if (value[index] == '(') {
                parentheses++;
            } else if (value[index] == ')') {
                parentheses = Math.Max(0, parentheses - 1);
            } else if (value[index] == '=' && parentheses == 0) {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTokenName(string name) {
        if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_')) {
            return false;
        }

        foreach (var character in name) {
            if (!char.IsLetterOrDigit(character) && character is not ('_' or '-')) {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<CssDeclaration> ParseDeclarations(
        string source,
        ref int index,
        List<CssDiagnostic> diagnostics) {
        var declarations = new List<CssDeclaration>();
        while (true) {
            SkipTrivia(source, ref index);
            if (index >= source.Length) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "Style rule is missing '}'.",
                    Math.Max(0, source.Length - 1),
                    1));
                return declarations;
            }

            if (source[index] == '}') {
                index++;
                return declarations;
            }

            var declarationStart = index;
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
                } else if ((character == ';' || character == '}') && parentheses == 0) {
                    break;
                }

                index++;
            }

            if (colon < 0) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    "MCSS declaration is missing ':'.",
                    declarationStart,
                    Math.Max(1, index - declarationStart)));
            } else {
                var name = StripComments(
                    source.Substring(declarationStart, colon - declarationStart)).Trim();
                var valueStart = colon + 1;
                var valueLength = index - valueStart;
                var rawValue = source.Substring(valueStart, valueLength);
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
                        declarationStart,
                        Math.Max(1, index - declarationStart)));
                } else {
                    declarations.Add(new CssDeclaration(
                        name,
                        value,
                        declarationStart,
                        valueStart + leading,
                        Math.Max(1, trailing - leading)));
                }
            }

            if (index < source.Length && source[index] == ';') {
                index++;
            }
        }
    }
}
