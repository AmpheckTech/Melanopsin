using System;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Schema.Discovery;
using Melanopsin.UI.Markup.Compiler.Styling;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Melanopsin.UI.Markup.Compiler.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Melanopsin.UI.Markup.Compiler.Generation;

[Generator(LanguageNames.CSharp)]
public sealed class MarkupGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var documents = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".mxml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => {
                var source = file.GetText(cancellationToken)?.ToString() ?? string.Empty;
                return MarkupParser.Parse(file.Path, source);
            });

        var collectedDocuments = documents.Collect();

        var manifests = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".mres", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => {
                var source = file.GetText(cancellationToken)?.ToString() ?? string.Empty;
                return ResourceManifestParser.Parse(file.Path, source);
            })
            .Collect()
            .Select(static (items, _) => ResourceManifestCatalog.Create(items));

        // Components that live entirely in markup are not part of the compilation yet, so they are
        // mirrored into throwaway syntax trees before the schema is discovered.
        var controls = context.CompilationProvider.Combine(collectedDocuments)
            .Select(static (input, cancellationToken) => {
                var shadows = MarkupComponentShadow.CreateAll(input.Right, input.Left);
                var compilation = shadows.Count == 0
                    ? input.Left
                    : input.Left.AddSyntaxTrees(shadows);
                return ControlCatalog.Create(compilation, cancellationToken);
            });

        var stylesheets = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".mcss", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => {
                var source = file.GetText(cancellationToken)?.ToString() ?? string.Empty;
                return CssParser.Parse(file.Path, source);
            })
            .Collect()
            .Select(static (items, _) => CssStylesheetCatalog.Create(items));

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(
            static (provider, _) => provider.GlobalOptions.TryGetValue(
                "build_property.RootNamespace",
                out var value)
                ? value
                : string.Empty);

        context.RegisterSourceOutput(controls, static (productionContext, catalog) => {
            foreach (var diagnostic in catalog.Diagnostics) {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    MarkupDiagnosticDescriptors.InvalidControlSchema,
                    Location.None,
                    diagnostic.Message));
            }
        });

        context.RegisterSourceOutput(manifests, static (productionContext, catalog) => {
            EmitResourceManifests(productionContext, catalog);
        });

        context.RegisterSourceOutput(
            stylesheets.Combine(rootNamespace),
            static (productionContext, input) =>
                EmitStylesheet(productionContext, input.Left, input.Right));

        context.RegisterSourceOutput(
            documents.Combine(manifests).Combine(controls).Combine(context.CompilationProvider),
            static (productionContext, input) =>
                Execute(
                    productionContext,
                    input.Left.Left.Left,
                    input.Left.Left.Right,
                    input.Left.Right,
                    input.Right));
    }

    private static void EmitStylesheet(
        SourceProductionContext context,
        CssStylesheetCatalog catalog,
        string rootNamespace) {
        foreach (var entry in catalog.Entries) {
            var stylesheet = entry.Stylesheet;
            var emission = CssEmitter.Emit(stylesheet, rootNamespace, entry.Imports);
            foreach (var diagnostic in emission.Diagnostics.Concat(entry.Diagnostics)) {
                context.ReportDiagnostic(Diagnostic.Create(
                    MarkupDiagnosticDescriptors.ForCode(diagnostic.Code),
                    CreateLocation(
                        stylesheet.Path,
                        stylesheet.Source,
                        diagnostic.Offset,
                        diagnostic.Length),
                    diagnostic.Message));
            }

            if (entry.IsValid && emission.Source is not null) {
                context.AddSource(
                    emission.HintName,
                    SourceText.From(emission.Source, System.Text.Encoding.UTF8));
            }
        }
    }

    private static void EmitResourceManifests(
        SourceProductionContext context,
        ResourceManifestCatalog catalog) {
        foreach (var manifest in catalog.Manifests) {
            foreach (var diagnostic in manifest.Diagnostics) {
                context.ReportDiagnostic(Diagnostic.Create(
                    MarkupDiagnosticDescriptors.ForCode(diagnostic.Code),
                    CreateLocation(
                        manifest.Path,
                        manifest.Source,
                        diagnostic.Offset,
                        diagnostic.Length),
                    diagnostic.Message));
            }

            if (!manifest.IsValid) {
                continue;
            }

            var source = ResourceManifestEmitter.Emit(manifest);
            context.AddSource(
                ResourceManifestEmitter.HintName(manifest),
                SourceText.From(source, System.Text.Encoding.UTF8));
        }
    }

    private static void Execute(
        SourceProductionContext context,
        MarkupDocument document,
        ResourceManifestCatalog resources,
        ControlCatalog controls,
        Compilation compilation) {
        var diagnostics = document.Diagnostics
            .Concat(MarkupValidator.Validate(document, controls))
            .Concat(MarkupComponentValidator.Validate(document, compilation))
            .Concat(MarkupResourceValidator.Validate(document, controls, resources))
            .ToArray();

        foreach (var diagnostic in diagnostics) {
            context.ReportDiagnostic(Diagnostic.Create(
                MarkupDiagnosticDescriptors.ForCode(diagnostic.Code),
                CreateLocation(
                    document.Path,
                    document.Source,
                    diagnostic.Offset,
                    diagnostic.Length),
                diagnostic.Message));
        }

        if (diagnostics.Length != 0) {
            return;
        }

        var className = MarkupNaming.ComponentClassName(document);
        var declaresComponent = compilation.GetTypeByMetadataName(
            $"{document.Namespace}.{className}") is null;
        var source = MarkupEmitter.Emit(document, controls, resources, declaresComponent);
        context.AddSource(HintName(document), SourceText.From(source, System.Text.Encoding.UTF8));
    }

    private static Location CreateLocation(
        string path,
        string source,
        int offset,
        int length) {
        var text = SourceText.From(source);
        var safeOffset = Math.Max(0, Math.Min(offset, text.Length));
        var safeLength = Math.Max(1, Math.Min(length, Math.Max(1, text.Length - safeOffset)));
        if (safeOffset == text.Length && text.Length > 0) {
            safeOffset--;
            safeLength = 1;
        }

        var span = new TextSpan(safeOffset, Math.Min(safeLength, text.Length - safeOffset));
        return Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
    }

    private static string HintName(MarkupDocument document) {
        var className = MarkupNaming.ComponentClassName(document);
        return $"{className}.{className}.g.cs";
    }
}
