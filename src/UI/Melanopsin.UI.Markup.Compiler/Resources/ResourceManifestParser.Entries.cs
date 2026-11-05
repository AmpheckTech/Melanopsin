// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed partial class ResourceManifestParser {
    private IReadOnlyList<ResourceDefinition> ParseRoot(XElement root) {
        var rootNamespace = root.Name.NamespaceName;
        if ((rootNamespace.Length != 0 && rootNamespace != ResourceNamespace) ||
            root.Name.LocalName is not "Resources" and not "ResourceManifest") {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                "The root element must be 'Resources' in the " +
                $"'{ResourceNamespace}' namespace.",
                root,
                1);
            return Array.Empty<ResourceDefinition>();
        }

        ReadMetadata(root);

        var definitions = new List<ResourceDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var categoryElement in root.Elements()) {
            var category = categoryElement.Name.LocalName;
            if (!TryGetCategory(category, out var entryName, out var kind, out var typeName)) {
                Report(
                    MarkupDiagnosticCodes.InvalidResourceManifest,
                    $"Unknown resource category '{category}'.",
                    categoryElement,
                    category.Length);
                continue;
            }

            foreach (var entry in categoryElement.Elements()) {
                var name = entry.Name.LocalName;
                if (!string.Equals(name, entryName, StringComparison.Ordinal)) {
                    Report(
                        MarkupDiagnosticCodes.InvalidResourceManifest,
                        $"Resource category '{category}' requires '{entryName}' entries.",
                        entry,
                        name.Length);
                    continue;
                }

                var identifier = ReadIdentifier(entry);
                if (identifier.Length == 0) {
                    continue;
                }

                var id = $"{category}.{identifier}";
                if (!seen.Add(id)) {
                    Report(
                        MarkupDiagnosticCodes.InvalidResourceManifest,
                        $"Resource '{id}' is declared more than once.",
                        entry,
                        entryName.Length);
                    continue;
                }

                var variants = ReadVariants(entry);
                definitions.Add(new ResourceDefinition(
                    category,
                    identifier,
                    id,
                    kind,
                    typeName,
                    variants));
            }
        }

        if (definitions.Count == 0 && _diagnostics.Count == 0) {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                "A resource manifest must declare at least one resource.",
                root,
                1);
        }

        return definitions;
    }

    private string ReadIdentifier(XElement entry) {
        var attribute = entry.Attribute("id");
        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value)) {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                "A resource entry must declare a non-empty id.",
                entry,
                1);
            return string.Empty;
        }

        var identifier = attribute.Value.Trim();
        if (!SyntaxFacts.IsValidIdentifier(identifier)) {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                $"'{identifier}' is not a valid resource id.",
                attribute,
                1);
            return string.Empty;
        }

        return identifier;
    }

    private IReadOnlyList<string> ReadVariants(XElement entry) {
        var attribute = entry.Attribute("variants");
        if (attribute is null) {
            return Array.Empty<string>();
        }

        if (!ResourceVariantNames.TryParse(
                attribute.Value,
                out var variants,
                out var error)) {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                error!,
                attribute,
                attribute.Name.LocalName.Length);
        }

        return variants;
    }

    private static bool TryGetCategory(
        string category,
        out string entryName,
        out string kind,
        out string typeName) {
        switch (category) {
            case "Strings":
                entryName = "String";
                kind = "String";
                typeName = "string";
                return true;
            case "Messages":
                entryName = "Message";
                kind = "Message";
                typeName = "string";
                return true;
            case "Images":
                entryName = "Image";
                kind = "Image";
                typeName = "global::Melanopsin.Graphics.ImageResource";
                return true;
            case "Fonts":
                entryName = "Font";
                kind = "Font";
                typeName = "global::Melanopsin.Graphics.Text.Fonts.FontFace";
                return true;
            case "Colors":
                entryName = "Color";
                kind = "Color";
                typeName = "global::Melanopsin.Graphics.Color";
                return true;
            case "Lengths":
                entryName = "Length";
                kind = "Length";
                typeName = "global::Melanopsin.UI.Length";
                return true;
            case "Numbers":
                entryName = "Number";
                kind = "Number";
                typeName = "double";
                return true;
            case "Binaries":
                entryName = "Binary";
                kind = "Binary";
                typeName = "byte[]";
                return true;
            default:
                entryName = string.Empty;
                kind = string.Empty;
                typeName = string.Empty;
                return false;
        }
    }
}
