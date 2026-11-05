// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.LanguageServer.Workspace;

internal static class SymbolLocator {
    private const string MarkupAttributeName = "Melanopsin.UI.Markup.MarkupAttributeAttribute";

    public static IReadOnlyList<Location> FindLocations(
        Compilation compilation,
        string typeName,
        string? memberName,
        string? attributeName,
        CancellationToken cancellationToken = default) {
        var type = FindType(compilation, typeName, cancellationToken);
        if (type is null) {
            // Not a type: the reference may point at a namespace, as the markup
            // "package" metadata does.
            return FindNamespaceLocations(compilation, typeName);
        }

        if (memberName is { Length: > 0 }) {
            for (var current = type; current is not null; current = current.BaseType) {
                var member = current.GetMembers(memberName).FirstOrDefault();
                if (member is not null) {
                    return SourceLocations(member);
                }
            }
        }

        if (attributeName is { Length: > 0 } &&
            FindMarkupAttributeLocation(type, attributeName) is { } location) {
            return [location];
        }

        return memberName is null ? SourceLocations(type) : Array.Empty<Location>();
    }

    private static IReadOnlyList<Location> FindNamespaceLocations(
        Compilation compilation,
        string qualifiedName) {
        var name = qualifiedName.StartsWith("global::", StringComparison.Ordinal)
            ? qualifiedName.Substring("global::".Length)
            : qualifiedName;
        if (name.Length == 0) {
            return Array.Empty<Location>();
        }

        INamespaceSymbol? current = compilation.GlobalNamespace;
        foreach (var segment in name.Split('.')) {
            current = current.GetNamespaceMembers()
                .FirstOrDefault(candidate => candidate.Name == segment);
            if (current is null) {
                return Array.Empty<Location>();
            }
        }

        return current.Locations.Where(static location => location.IsInSource).ToArray();
    }

    private static Location? FindMarkupAttributeLocation(
        INamedTypeSymbol type,
        string attributeName) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var attribute in current.GetAttributes()) {
                if (attribute.AttributeClass?.ToDisplayString() != MarkupAttributeName ||
                    attribute.ConstructorArguments.Length == 0 ||
                    attribute.ConstructorArguments[0].Value is not string name ||
                    !string.Equals(name, attributeName, StringComparison.Ordinal)) {
                    continue;
                }

                if (attribute.ApplicationSyntaxReference is { } reference) {
                    return reference.GetSyntax().GetLocation();
                }
            }
        }

        return null;
    }

    private static INamedTypeSymbol? FindType(
        Compilation compilation,
        string typeName,
        CancellationToken cancellationToken) {
        var name = typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : typeName;
        var direct = compilation.GetTypeByMetadataName(name);
        if (direct is not null) {
            return direct;
        }

        var shortName = name.Substring(name.LastIndexOf('.') + 1);
        var symbol = compilation.GetSymbolsWithName(
                candidate => candidate == shortName,
                SymbolFilter.Type,
                cancellationToken)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(candidate =>
                candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == typeName);
        if (symbol is not null) {
            return symbol;
        }

        foreach (var reference in compilation.References) {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) {
                continue;
            }

            var found = FindType(assembly.GlobalNamespace, typeName, cancellationToken);
            if (found is not null) {
                return found;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? FindType(
        INamespaceSymbol root,
        string displayName,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var type in root.GetTypeMembers()) {
            if (FindNested(type, displayName, cancellationToken) is { } found) {
                return found;
            }
        }

        foreach (var child in root.GetNamespaceMembers()) {
            if (FindType(child, displayName, cancellationToken) is { } found) {
                return found;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? FindNested(
        INamedTypeSymbol type,
        string displayName,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == displayName) {
            return type;
        }

        foreach (var nested in type.GetTypeMembers()) {
            if (FindNested(nested, displayName, cancellationToken) is { } found) {
                return found;
            }
        }

        return null;
    }

    private static IReadOnlyList<Location> SourceLocations(ISymbol symbol) {
        return symbol.Locations
            .Where(static location => location.IsInSource)
            .ToArray();
    }
}
