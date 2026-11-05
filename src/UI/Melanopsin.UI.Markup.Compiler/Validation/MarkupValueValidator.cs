// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static class MarkupValueValidator {
    public static void ValidateStaticValue(
        MarkupElement element,
        MarkupAttribute attribute,
        MarkupAttributeDefinition? definition,
        List<MarkupDiagnostic> diagnostics) {
        if (definition is null ||
            attribute.Parts.Any(static part => part.IsExpression || part.IsResource) ||
            attribute.Parts.Count != 1) {
            return;
        }

        var value = attribute.Parts[0].Value;
        if (definition.ValueKind == SchemaValueKind.Number &&
            !float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out _)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Length &&
            !IsValidLength(value)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Color &&
            !IsValidColor(value)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Integer &&
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Boolean && !bool.TryParse(value, out _)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Enum &&
            !definition.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase)) {
            ReportValueError(element, attribute, value, diagnostics);
        }

        if (definition.ValueKind == SchemaValueKind.Input &&
            !value.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("auto", StringComparison.OrdinalIgnoreCase)) {
            ReportValueError(element, attribute, value, diagnostics);
        }
    }

    private static void ReportValueError(
        MarkupElement element,
        MarkupAttribute attribute,
        string value,
        List<MarkupDiagnostic> diagnostics) {
        diagnostics.Add(new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidAttributeValue,
            $"Invalid value '{value}' for {element.Name}.{attribute.Name}.",
            attribute.Offset,
            attribute.Name.Length));
    }

    private static bool IsValidLength(string value) {
        if (string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        var normalized = value.Trim();
        var suffixes = new[] {
            "rem", "su", "ru", "fu", "iu", "hx", "vw", "vh", "fr", "em", "dp", "sp", "pt", "px", "%",
        };
        foreach (var suffix in suffixes) {
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            normalized = normalized.Substring(0, normalized.Length - suffix.Length);
            break;
        }

        return float.TryParse(
                   normalized,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var length) &&
               length >= 0;
    }

    private static bool IsValidColor(string value) {
        value = value.Trim();
        return string.Equals(value, "transparent", StringComparison.OrdinalIgnoreCase) ||
            value.Length == 7 && value[0] == '#' &&
            byte.TryParse(value.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) &&
            byte.TryParse(value.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) &&
            byte.TryParse(value.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }
}
