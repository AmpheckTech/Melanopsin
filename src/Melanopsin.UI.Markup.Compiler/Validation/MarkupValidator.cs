using System;
using System.Globalization;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static class MarkupValidator {
    public static IReadOnlyList<MarkupDiagnostic> Validate(
        MarkupDocument document,
        ControlCatalog controls) {
        var diagnostics = new List<MarkupDiagnostic>();
        if (document.Namespace is { Length: > 0 } &&
            SyntaxFactory.ParseName(document.Namespace).ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"'{document.Namespace}' is not a valid C# namespace.",
                0,
                1));
        }

        if (document.ResourcePackage is { Length: > 0 } &&
            SyntaxFactory.ParseName(document.ResourcePackage).ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"'{document.ResourcePackage}' is not a valid resource package name.",
                0,
                1));
        }

        var referenceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document.Nodes) {
            ValidateNode(
                controls,
                node,
                isDynamic: false,
                referenceNames,
                diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateNode(
        ControlCatalog controls,
        MarkupNode node,
        bool isDynamic,
        HashSet<string> referenceNames,
        List<MarkupDiagnostic> diagnostics) {
        switch (node) {
            case MarkupElement element:
                ValidateElement(
                    controls,
                    element,
                    isDynamic,
                    referenceNames,
                    diagnostics);
                foreach (var child in element.Children) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupExpression expression:
                ValidateExpression(expression.Expression, expression.Offset, diagnostics);
                break;

            case MarkupIf conditional:
                ValidateExpression(conditional.Condition, conditional.Offset, diagnostics);
                foreach (var child in conditional.WhenTrue) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                foreach (var child in conditional.WhenFalse) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupFor loop:
                ValidateExpression(loop.Iterable, loop.Offset, diagnostics);
                if (loop.Track is { Length: > 0 }) {
                    ValidateExpression(loop.Track, loop.Offset, diagnostics);
                }

                foreach (var child in loop.Body) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                foreach (var child in loop.EmptyBody) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupSwitch switchBlock:
                ValidateExpression(
                    switchBlock.Expression,
                    switchBlock.Offset,
                    diagnostics);
                foreach (var switchCase in switchBlock.Cases) {
                    ValidateExpression(
                        switchCase.Pattern,
                        switchCase.Offset,
                        diagnostics);
                    foreach (var child in switchCase.Body) {
                        ValidateNode(
                            controls,
                            child,
                            isDynamic: true,
                            referenceNames,
                            diagnostics);
                    }
                }

                foreach (var child in switchBlock.DefaultBody) {
                    ValidateNode(
                        controls,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;
        }
    }

    private static void ValidateElement(
        ControlCatalog controls,
        MarkupElement element,
        bool isDynamic,
        HashSet<string> referenceNames,
        List<MarkupDiagnostic> diagnostics) {
        var control = controls.Find(element.Name);
        if (control is null) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.UnknownControl,
                $"Unknown control '{element.Name}'.",
                element.Offset,
                Math.Max(1, element.Name.Length + 1)));
        }

        foreach (var attribute in element.Attributes) {
            if (MarkupSchema.IsReferenceAttribute(attribute)) {
                ValidateReference(
                    element,
                    attribute,
                    isDynamic,
                    referenceNames,
                    diagnostics);
                continue;
            }

            var definition = control is not null &&
                             control.Attributes.TryGetValue(attribute.Name, out var found)
                ? found
                : null;
            if (control is not null && definition is null) {
                diagnostics.Add(new MarkupDiagnostic(
                    MarkupDiagnosticCodes.UnknownAttribute,
                    $"Control '{element.Name}' does not support attribute '{attribute.Name}'.",
                    attribute.Offset,
                    attribute.Name.Length));
            }

            foreach (var part in attribute.Parts.Where(static part => part.IsExpression)) {
                ValidateExpression(part.Value, attribute.Offset, diagnostics);
            }

            ValidateStaticValue(element, attribute, definition, diagnostics);
        }
    }

    private static void ValidateReference(
        MarkupElement element,
        MarkupAttribute attribute,
        bool isDynamic,
        HashSet<string> referenceNames,
        List<MarkupDiagnostic> diagnostics) {
        if (!MarkupReference.TryParse(attribute, out var reference)) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidReference,
                $"Reference on <{element.Name}> must be a valid C# identifier.",
                attribute.Offset,
                Math.Max(1, attribute.Name.Length)));
            return;
        }

        if (isDynamic) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidReference,
                $"Reference '{reference.Name}' cannot be declared inside conditional or repeated markup.",
                attribute.Offset,
                Math.Max(1, attribute.Name.Length)));
            return;
        }

        if (!referenceNames.Add(reference.Name)) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidReference,
                $"Reference '{reference.Name}' is declared more than once.",
                attribute.Offset,
                Math.Max(1, attribute.Name.Length)));
        }
    }

    private static void ValidateStaticValue(
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

    private static void ValidateExpression(
        string expression,
        int offset,
        List<MarkupDiagnostic> diagnostics) {
        if (expression.Length == 0) {
            return;
        }

        var parsed = SyntaxFactory.ParseExpression(expression);
        if (parsed.IsMissing || parsed.ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidExpression,
                $"Invalid C# expression '{expression}'.",
                offset,
                1));
        }
    }
}
