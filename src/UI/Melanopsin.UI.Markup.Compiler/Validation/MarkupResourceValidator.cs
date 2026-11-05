// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static class MarkupResourceValidator {
    public static IReadOnlyList<MarkupDiagnostic> Validate(
        MarkupDocument document,
        ControlCatalog controls,
        ResourceManifestCatalog manifests) {
        var diagnostics = new List<MarkupDiagnostic>();
        var resolver = new MarkupControlResolver(document, controls);
        var resources = new MarkupResourceResolver(manifests);
        var resourcePackage = document.ResourcePackage ?? document.Namespace;
        foreach (var node in document.Nodes) {
            ValidateNode(node, resolver, resources, resourcePackage, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateNode(
        MarkupNode node,
        MarkupControlResolver resolver,
        MarkupResourceResolver resources,
        string? package,
        List<MarkupDiagnostic> diagnostics) {
        switch (node) {
            case MarkupElement element:
                ValidateElement(element, resolver, resources, package, diagnostics);
                foreach (var child in element.Children) {
                    ValidateNode(child, resolver, resources, package, diagnostics);
                }

                break;

            case MarkupIf conditional:
                ValidateNodes(
                    conditional.WhenTrue,
                    resolver,
                    resources,
                    package,
                    diagnostics);
                ValidateNodes(
                    conditional.WhenFalse,
                    resolver,
                    resources,
                    package,
                    diagnostics);
                break;

            case MarkupFor loop:
                ValidateNodes(loop.Body, resolver, resources, package, diagnostics);
                ValidateNodes(loop.EmptyBody, resolver, resources, package, diagnostics);
                break;

            case MarkupSwitch switchBlock:
                foreach (var switchCase in switchBlock.Cases) {
                    ValidateNodes(
                        switchCase.Body,
                        resolver,
                        resources,
                        package,
                        diagnostics);
                }

                ValidateNodes(
                    switchBlock.DefaultBody,
                    resolver,
                    resources,
                    package,
                    diagnostics);
                break;
        }
    }

    private static void ValidateNodes(
        IReadOnlyList<MarkupNode> nodes,
        MarkupControlResolver resolver,
        MarkupResourceResolver resources,
        string? package,
        List<MarkupDiagnostic> diagnostics) {
        foreach (var node in nodes) {
            ValidateNode(node, resolver, resources, package, diagnostics);
        }
    }

    private static void ValidateElement(
        MarkupElement element,
        MarkupControlResolver resolver,
        MarkupResourceResolver resources,
        string? package,
        List<MarkupDiagnostic> diagnostics) {
        var control = resolver.Find(element);
        foreach (var attribute in element.Attributes) {
            var resourceParts = attribute.Parts
                .Where(static part => part.IsResource)
                .ToArray();
            if (resourceParts.Length == 0) {
                continue;
            }

            if (attribute.Parts.Count != 1 || resourceParts.Length != 1) {
                diagnostics.Add(Error(
                    MarkupDiagnosticCodes.InvalidResourceReference,
                    "A resource reference must be the only value of an attribute.",
                    attribute));
                continue;
            }

            if (!ResourceReference.TryParse(resourceParts[0].Value, out var reference)) {
                diagnostics.Add(Error(
                    MarkupDiagnosticCodes.InvalidResourceReference,
                    $"Invalid resource reference '{resourceParts[0].Value}'.",
                    attribute));
                continue;
            }

            if (!resources.TryResolve(package, reference, out var resource)) {
                var message = string.IsNullOrWhiteSpace(package)
                    ? $"Resource '{reference.Id}' cannot be resolved because the XML view has no package."
                    : $"Resource '{reference.Id}' is not declared by the resource manifest for '{package}'.";
                diagnostics.Add(Error(
                    MarkupDiagnosticCodes.UnknownResource,
                    message,
                    attribute));
                continue;
            }

            if (control is null ||
                !control.Attributes.TryGetValue(attribute.Name, out var definition)) {
                continue;
            }

            if (!IsCompatible(attribute, definition, resource.Definition)) {
                diagnostics.Add(Error(
                    MarkupDiagnosticCodes.IncompatibleResourceType,
                    $"Resource '{reference.Id}' cannot be assigned to " +
                    $"{element.QualifiedName}.{attribute.Name}.",
                    attribute));
            }
        }
    }

    private static bool IsCompatible(
        MarkupAttribute attribute,
        MarkupAttributeDefinition definition,
        ResourceDefinition resource) {
        if (InlineLengthProperty.TryGet(attribute.Name, out _)) {
            return string.Equals(resource.Kind, "Length", StringComparison.Ordinal) &&
                   IsType(resource.ValueTypeName, "global::Melanopsin.UI.Length");
        }

        if (attribute.Name == "font") {
            return string.Equals(resource.Kind, "Font", StringComparison.Ordinal) &&
                   IsType(
                       resource.ValueTypeName,
                       "global::Melanopsin.Graphics.Text.Fonts.FontFace");
        }

        if (definition.ValueTypeName is { Length: > 0 } expectedType) {
            return IsObject(expectedType) ||
                   IsType(resource.ValueTypeName, expectedType);
        }

        return definition.ValueKind switch {
            SchemaValueKind.Any => resource.Kind is "String" or "Message",
            SchemaValueKind.Length => resource.Kind == "Length",
            SchemaValueKind.Number => resource.Kind == "Number",
            _ => false,
        };
    }

    private static bool IsObject(string typeName) {
        return NormalizeTypeName(typeName) == "global::System.Object";
    }

    private static bool IsType(string actualTypeName, string expectedTypeName) {
        return string.Equals(
            NormalizeTypeName(actualTypeName),
            NormalizeTypeName(expectedTypeName),
            StringComparison.Ordinal);
    }

    private static string NormalizeTypeName(string typeName) {
        var normalized = typeName.Trim().TrimEnd('?');
        return normalized switch {
            "string" or "String" or "System.String" => "global::System.String",
            "object" or "Object" or "System.Object" => "global::System.Object",
            "byte[]" or "System.Byte[]" => "byte[]",
            _ => normalized,
        };
    }

    private static MarkupDiagnostic Error(
        string code,
        string message,
        MarkupAttribute attribute) {
        return new MarkupDiagnostic(
            code,
            message,
            attribute.Offset,
            Math.Max(1, attribute.Name.Length));
    }
}
