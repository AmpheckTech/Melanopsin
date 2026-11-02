using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Schema.Discovery;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Melanopsin.UI.Markup.Compiler.Validation;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// Editor-facing analysis built on the same parser, schema and validators used by
/// the compile-time generator.
/// </summary>
public static partial class MarkupLanguageService {
    private static readonly ConditionalWeakTable<Compilation, Lazy<ControlCatalog>>
        ControlCatalogs = new();

    public static MarkupAnalysis Analyze(
        string path,
        string source,
        Compilation compilation,
        CancellationToken cancellationToken = default) {
        var controls = GetControlCatalog(compilation, cancellationToken);
        return Analyze(path, source, compilation, controls, cancellationToken);
    }

    private static MarkupAnalysis Analyze(
        string path,
        string source,
        Compilation compilation,
        ControlCatalog controls,
        CancellationToken cancellationToken) {
        var document = MarkupParser.Parse(path, source);
        var diagnostics = new List<MarkupDiagnosticInfo>();
        diagnostics.AddRange(document.Diagnostics.Select(ToInfo));
        diagnostics.AddRange(controls.Diagnostics.Select(static diagnostic =>
            new MarkupDiagnosticInfo(diagnostic.Code, diagnostic.Message, 0, 1)));
        diagnostics.AddRange(MarkupValidator.Validate(document, controls).Select(ToInfo));
        diagnostics.AddRange(MarkupComponentValidator
            .Validate(document, compilation)
            .Select(ToInfo));

        var resolver = new MarkupControlResolver(document, controls);
        var elements = document.Nodes
            .OfType<MarkupElement>()
            .Select(element => ToInfo(element, resolver))
            .ToArray();
        var componentType = string.IsNullOrWhiteSpace(document.Namespace) ||
                            string.IsNullOrWhiteSpace(document.ComponentName)
            ? null
            : compilation.GetTypeByMetadataName(
                document.Namespace + "." + document.ComponentName);

        return new MarkupAnalysis(
            path,
            document.Namespace,
            document.ComponentName,
            componentType is null
                ? null
                : new MarkupDefinitionTarget(
                    componentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            diagnostics,
            elements,
            document.CodeBlocks
                .Select(static block => new MarkupCodeBlockInfo(block.Offset, block.Text.Length))
                .ToArray());
    }

    public static IReadOnlyList<MarkupCompletionItem> GetCompletions(
        string path,
        string source,
        int offset,
        Compilation compilation,
        CancellationToken cancellationToken = default) {
        var controls = GetControlCatalog(compilation, cancellationToken);
        var analysis = Analyze(path, source, compilation, controls, cancellationToken);
        return GetCompletions(source, offset, analysis, controls, compilation, cancellationToken);
    }

    public static MarkupHoverInfo? GetHover(
        string path,
        string source,
        int offset,
        Compilation compilation,
        CancellationToken cancellationToken = default) {
        var analysis = Analyze(path, source, compilation, cancellationToken);
        return GetHover(source, offset, analysis, compilation, cancellationToken);
    }

    public static IReadOnlyList<MarkupSemanticToken> GetSemanticTokens(
        string path,
        string source,
        Compilation compilation,
        CancellationToken cancellationToken = default) {
        var analysis = Analyze(path, source, compilation, cancellationToken);
        return GetSemanticTokens(path, source, analysis, compilation, cancellationToken);
    }

    private static MarkupDiagnosticInfo ToInfo(MarkupDiagnostic diagnostic) {
        return new MarkupDiagnosticInfo(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Offset,
            Math.Max(1, diagnostic.Length));
    }

    /// <summary>
    /// Returns a compilation that also declares the components which exist only in markup, so schema
    /// discovery, completions and hover resolve for them. The added declarations back symbol queries
    /// only and are never compiled.
    /// </summary>
    public static Compilation AddMarkupComponents(
        Compilation compilation,
        IReadOnlyList<MarkupSourceFile> documents) {
        if (documents.Count == 0) {
            return compilation;
        }

        var trees = new List<SyntaxTree>();
        foreach (var document in documents) {
            var parsed = MarkupParser.Parse(document.Path, document.Text);
            if (MarkupComponentShadow.Create(parsed, compilation) is { } tree) {
                trees.Add(tree);
            }
        }

        return trees.Count == 0 ? compilation : compilation.AddSyntaxTrees(trees);
    }

    private static ControlCatalog GetControlCatalog(
        Compilation compilation,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var catalog = ControlCatalogs.GetValue(
            compilation,
            static value => new Lazy<ControlCatalog>(
                () => ControlCatalog.Create(value, CancellationToken.None),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return catalog.Value;
    }

    private static MarkupElementInfo ToInfo(
        MarkupElement element,
        MarkupControlResolver resolver) {
        var control = resolver.Find(element);
        var attributes = element.Attributes
            .Select(attribute => ToInfo(attribute, control))
            .ToArray();
        return new MarkupElementInfo(
            element.Name,
            element.QualifiedName,
            element.NamespaceUri,
            element.Offset,
            element.QualifiedName.Length,
            control is null ? null : ToInfo(control),
            attributes,
            element.Children
                .SelectMany(EnumerateMarkupElements)
                .Select(child => ToInfo(child, resolver))
                .ToArray());
    }

    private static IEnumerable<MarkupElement> EnumerateMarkupElements(MarkupNode node) {
        switch (node) {
            case MarkupElement element:
                yield return element;
                yield break;
            case MarkupIf conditional:
                foreach (var child in conditional.WhenTrue.Concat(conditional.WhenFalse)) {
                    foreach (var element in EnumerateMarkupElements(child)) {
                        yield return element;
                    }
                }

                yield break;
            case MarkupFor loop:
                foreach (var child in loop.Body.Concat(loop.EmptyBody)) {
                    foreach (var element in EnumerateMarkupElements(child)) {
                        yield return element;
                    }
                }

                yield break;
            case MarkupSwitch switchNode:
                foreach (var child in switchNode.Cases
                             .SelectMany(static branch => branch.Body)
                             .Concat(switchNode.DefaultBody)) {
                    foreach (var element in EnumerateMarkupElements(child)) {
                        yield return element;
                    }
                }

                yield break;
        }
    }

    private static MarkupControlInfo ToInfo(ControlDefinition control) {
        return new MarkupControlInfo(
            control.Name,
            control.Namespace,
            control.AssemblyName,
            control.TypeName,
            control.IsComponent,
            control.Attributes.Values
                .OrderBy(static attribute => attribute.Name, StringComparer.Ordinal)
                .Select(attribute => new MarkupAttributeInfo(
                    attribute.Name,
                    0,
                    0,
                    attribute.ValueTypeName,
                    attribute.ValueKind.ToString(),
                    attribute.AllowedValues.OrderBy(
                            static value => value,
                            StringComparer.Ordinal)
                        .ToArray(),
                    GetAttributeDefinition(control, attribute)))
                .ToArray(),
            control.Events.Values
                .OrderBy(static markupEvent => markupEvent.Name, StringComparer.Ordinal)
                .Select(static markupEvent => new MarkupEventInfo(
                    markupEvent.Name,
                    markupEvent.DelegateTypeName,
                    markupEvent.ParameterCount,
                    markupEvent.ArgumentTypeName))
                .ToArray());
    }

    private static MarkupAttributeInfo ToInfo(
        MarkupAttribute attribute,
        ControlDefinition? control) {
        MarkupAttributeDefinition? schema = null;
        if (control is not null) {
            control.Attributes.TryGetValue(attribute.Name, out schema);
        }
        return new MarkupAttributeInfo(
            attribute.Name,
            attribute.Offset,
            attribute.Name.Length,
            schema?.ValueTypeName,
            schema?.ValueKind.ToString() ?? "Unknown",
            schema?.AllowedValues.OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>(),
            schema is null || control is null
                ? null
                : GetAttributeDefinition(control, schema));
    }

    private static MarkupDefinitionTarget? GetAttributeDefinition(
        ControlDefinition control,
        MarkupAttributeDefinition attribute) {
        var owner = attribute.DeclaringTypeName is { Length: > 0 } declaringTypeName
            ? declaringTypeName
            : control.TypeName;

        if (attribute.DeclaredPropertyName is { Length: > 0 } declaredPropertyName) {
            return new MarkupDefinitionTarget(owner, declaredPropertyName);
        }

        if (attribute.PropertyName is { Length: > 0 } propertyName) {
            return new MarkupDefinitionTarget(owner, propertyName);
        }

        if (attribute.DescriptorAccessor is { Length: > 0 } accessor) {
            var separator = accessor.LastIndexOf('.');
            return separator < 0
                ? null
                : new MarkupDefinitionTarget(
                    accessor.Substring(0, separator),
                    accessor.Substring(separator + 1));
        }

        // Attributes declared on the control itself (for example "width" or "style") have no
        // backing member, so point at the [MarkupAttribute] declaration instead.
        return new MarkupDefinitionTarget(
            owner,
            MemberName: null,
            AttributeName: attribute.Name);
    }
}
