using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Schema.Discovery;

internal static class ControlTypeDiscovery {
    public static IEnumerable<INamedTypeSymbol> Enumerate(
        Compilation compilation,
        CancellationToken cancellationToken) {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var element = compilation.GetTypeByMetadataName(MarkupMetadataNames.Element);
        if (element is null) {
            yield break;
        }

        foreach (var syntaxTree in compilation.SyntaxTrees) {
            cancellationToken.ThrowIfCancellationRequested();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var declaration in syntaxTree.GetRoot(cancellationToken)
                         .DescendantNodes()
                         .OfType<TypeDeclarationSyntax>()) {
                if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is INamedTypeSymbol type &&
                    HasMarkupAttribute(type) &&
                    seen.Add(type)) {
                    yield return type;
                }
            }
        }

        foreach (var reference in compilation.References) {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly ||
                !ReferencesElement(assembly)) {
                continue;
            }

            foreach (var type in EnumerateTypes(assembly.GlobalNamespace, cancellationToken)) {
                if (HasMarkupAttribute(type) && seen.Add(type)) {
                    yield return type;
                }
            }
        }
    }

    private static bool ReferencesElement(IAssemblySymbol assembly) {
        return assembly.GetTypeByMetadataName(MarkupMetadataNames.Element) is not null ||
               assembly.Modules
                   .SelectMany(static module => module.ReferencedAssemblySymbols)
                   .Any(static reference =>
                       reference.Identity.Name == MarkupMetadataNames.ElementAssembly);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(
        INamespaceSymbol namespaceSymbol,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var type in namespaceSymbol.GetTypeMembers()) {
            foreach (var nested in EnumerateNestedTypes(type, cancellationToken)) {
                yield return nested;
            }
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers()) {
            foreach (var type in EnumerateTypes(child, cancellationToken)) {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(
        INamedTypeSymbol type,
        CancellationToken cancellationToken) {
        yield return type;
        foreach (var nested in type.GetTypeMembers()) {
            foreach (var result in EnumerateNestedTypes(nested, cancellationToken)) {
                yield return result;
            }
        }
    }

    private static bool HasMarkupAttribute(INamedTypeSymbol type) {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() is
                MarkupMetadataNames.MarkupElementAttribute or
                MarkupMetadataNames.MarkupComponentAttribute);
    }
}
