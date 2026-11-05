using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static class MarkupComponentValidator {
    public static IReadOnlyList<MarkupDiagnostic> Validate(
        MarkupDocument document,
        Compilation compilation) {
        if (document.Namespace is not { Length: > 0 } namespaceName ||
            document.ComponentName is not { Length: > 0 } componentName) {
            return Array.Empty<MarkupDiagnostic>();
        }

        var metadataName = $"{namespaceName}.{componentName}";
        var component = compilation.GetTypeByMetadataName(metadataName);
        if (component is null) {
            // A component that has no C# declaration is materialized by the generator, so the
            // view can live entirely in markup.
            return Array.Empty<MarkupDiagnostic>();
        }

        if (component.TypeKind != TypeKind.Class ||
            component.IsStatic ||
            component.Arity != 0) {
            return
            [
                Error(
                    $"Component '{metadataName}' must be a non-static, non-generic class.",
                    0),
            ];
        }

        var isPartial = component.DeclaringSyntaxReferences.Any(static reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        if (!isPartial) {
            return
            [
                Error(
                    $"Component class '{metadataName}' must use the partial modifier.",
                    0),
            ];
        }

        var componentBase = compilation.GetTypeByMetadataName(
            "Melanopsin.UI.Component");
        if (componentBase is null || !DerivesFrom(component, componentBase)) {
            return
            [
                Error(
                    $"Component class '{metadataName}' must derive from Melanopsin.UI.Component.",
                    0),
            ];
        }

        return ValidateReferences(document, component);
    }

    private static IReadOnlyList<MarkupDiagnostic> ValidateReferences(
        MarkupDocument document,
        INamedTypeSymbol component) {
        var diagnostics = new List<MarkupDiagnostic>();
        foreach (var node in document.Nodes) {
            ValidateReferenceNode(node, component, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateReferenceNode(
        MarkupNode node,
        INamedTypeSymbol component,
        List<MarkupDiagnostic> diagnostics) {
        switch (node) {
            case MarkupElement element:
                foreach (var attribute in element.Attributes) {
                    if (!MarkupSchema.IsReferenceAttribute(attribute) ||
                        !MarkupReference.TryParse(attribute, out var reference) ||
                        !HasMember(component, reference.Name)) {
                        continue;
                    }

                    diagnostics.Add(new MarkupDiagnostic(
                        MarkupDiagnosticCodes.InvalidReference,
                        $"Reference '{reference.Name}' conflicts with an existing member of " +
                        $"'{component.Name}'.",
                        attribute.Offset,
                        Math.Max(1, attribute.Name.Length)));
                }

                foreach (var child in element.Children) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                break;

            case MarkupIf conditional:
                foreach (var child in conditional.WhenTrue) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                foreach (var child in conditional.WhenFalse) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                break;

            case MarkupFor loop:
                foreach (var child in loop.Body) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                foreach (var child in loop.EmptyBody) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                break;

            case MarkupSwitch switchBlock:
                foreach (var switchCase in switchBlock.Cases) {
                    foreach (var child in switchCase.Body) {
                        ValidateReferenceNode(child, component, diagnostics);
                    }
                }

                foreach (var child in switchBlock.DefaultBody) {
                    ValidateReferenceNode(child, component, diagnostics);
                }

                break;
        }
    }

    private static bool HasMember(INamedTypeSymbol component, string name) {
        for (var type = component; type is not null; type = type.BaseType) {
            if (type.GetMembers(name).Length != 0) {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) {
                return true;
            }
        }

        return false;
    }

    private static MarkupDiagnostic Error(string message, int offset) {
        return new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidComponentClass,
            message,
            offset,
            1);
    }
}
