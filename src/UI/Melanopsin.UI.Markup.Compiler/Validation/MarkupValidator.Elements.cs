// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static partial class MarkupValidator {
    private static void ValidateElement(
        MarkupControlResolver resolver,
        MarkupElement element,
        bool isDynamic,
        HashSet<string> referenceNames,
        List<MarkupDiagnostic> diagnostics) {
        var resolution = resolver.Resolve(element);
        var control = resolution.Control;
        if (resolution.Diagnostic is { } resolutionDiagnostic) {
            diagnostics.Add(resolutionDiagnostic);
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

            if (MarkupEventBinding.TryGetEventName(attribute, out var eventName)) {
                ValidateEvent(
                    element,
                    control,
                    eventName,
                    attribute,
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
                    $"Control '{element.QualifiedName}' does not support attribute '{attribute.Name}'.",
                    attribute.Offset,
                    attribute.Name.Length));
            }

            foreach (var part in attribute.Parts.Where(static part => part.IsExpression)) {
                ValidateExpression(part.Value, attribute.Offset, diagnostics);
            }

            MarkupValueValidator.ValidateStaticValue(
                element,
                attribute,
                definition,
                diagnostics);
        }
    }

    private static void ValidateEvent(
        MarkupElement element,
        ControlDefinition? control,
        string eventName,
        MarkupAttribute attribute,
        List<MarkupDiagnostic> diagnostics) {
        if (control is null) {
            return;
        }

        if (!control.Events.TryGetValue(eventName, out var eventDefinition)) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.UnknownEvent,
                $"Control '{element.QualifiedName}' does not define event '{eventName}'.",
                attribute.Offset,
                attribute.Name.Length));
            return;
        }

        if (!MarkupEventBinding.TryGetStatement(attribute, out var statement) ||
            string.IsNullOrWhiteSpace(statement)) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidEventExpression,
                $"Event '{element.QualifiedName}.{eventName}' requires a C# statement.",
                attribute.Offset,
                attribute.Name.Length));
            return;
        }

        if (eventDefinition.ParameterCount > 1) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidEventExpression,
                $"Event '{element.QualifiedName}.{eventName}' has multiple parameters. " +
                "Event bindings currently support zero or one parameter.",
                attribute.Offset,
                attribute.Name.Length));
            return;
        }

        var rewritten = MarkupEventBinding.RewriteEventArgument(
            statement,
            out var usesEventArgument);
        if (usesEventArgument && eventDefinition.ParameterCount == 0) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidEventExpression,
                $"Event '{element.QualifiedName}.{eventName}' has no argument, so '$event' cannot be used.",
                attribute.Offset,
                attribute.Name.Length));
            return;
        }

        var statementForParsing = rewritten.TrimEnd().EndsWith(
            ";",
            StringComparison.Ordinal)
            ? rewritten
            : rewritten + ";";
        var parsed = SyntaxFactory.ParseStatement(statementForParsing);
        if (parsed.IsMissing || parsed.ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidEventExpression,
                $"Invalid C# event statement '{statement}' for " +
                $"'{element.QualifiedName}.{eventName}'.",
                attribute.Offset,
                attribute.Name.Length));
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
                $"Reference on <{element.QualifiedName}> must be a valid C# identifier.",
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
}
