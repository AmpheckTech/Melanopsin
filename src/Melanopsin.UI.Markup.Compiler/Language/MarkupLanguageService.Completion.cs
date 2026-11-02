using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Schema;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class MarkupLanguageService {
    private static IReadOnlyList<MarkupCompletionItem> GetCompletions(
        string source,
        int offset,
        MarkupAnalysis analysis,
        ControlCatalog controls,
        Compilation compilation,
        CancellationToken cancellationToken) {
        if (InsideCodeBlock(analysis, offset)) {
            // A @code block is plain C#: the component's members are the candidates, and the
            // component may address its own private ones.
            var owner = FindComponent(analysis, compilation, cancellationToken);
            return owner is null
                ? Array.Empty<MarkupCompletionItem>()
                : CompleteMembers(owner, cancellationToken, includePrivate: true);
        }

        if (FindInterpolationStart(source, offset) >= 0) {
            var component = FindComponent(analysis, compilation, cancellationToken);
            return component is null
                ? Array.Empty<MarkupCompletionItem>()
                : CompleteMembers(component, cancellationToken);
        }

        if (!TryFindOpenTag(source, offset, out var tag)) {
            return CompleteMarkupItems(analysis, controls);
        }

        if (offset <= tag.NameEnd) {
            return CompleteControls(analysis, controls);
        }

        var attribute = tag.Attributes.FirstOrDefault(candidate =>
            Contains(candidate.NameStart, candidate.NameEnd - candidate.NameStart, offset) ||
            candidate.ValueStart >= 0 &&
            Contains(candidate.ValueStart, candidate.ValueEnd - candidate.ValueStart, offset));
        var tagName = source.Substring(tag.NameStart, tag.NameEnd - tag.NameStart);
        var control = FindControl(analysis, controls, tagName);
        if (control is null) {
            return Array.Empty<MarkupCompletionItem>();
        }

        if (attribute.NameEnd > attribute.NameStart &&
            attribute.ValueStart >= 0 &&
            offset >= attribute.ValueStart) {
            return CompleteAttributeValue(
                source,
                attribute,
                control,
                analysis,
                compilation,
                cancellationToken);
        }

        var isRoot = analysis.Elements.Count > 0 &&
                     analysis.Elements[0].Offset == tag.NameStart;
        return CompleteAttributes(source, control, tag.Attributes, isRoot);
    }

    private static IReadOnlyList<MarkupCompletionItem> CompleteAttributeValue(
        string source,
        AttributeSpan attribute,
        MarkupControlInfo control,
        MarkupAnalysis analysis,
        Compilation compilation,
        CancellationToken cancellationToken) {
        var name = source.Substring(
            attribute.NameStart,
            attribute.NameEnd - attribute.NameStart);
        var schema = control.Attributes.FirstOrDefault(candidate => candidate.Name == name);
        var eventName = name.Length > 2 &&
                        name.StartsWith("on", StringComparison.Ordinal) &&
                        char.IsUpper(name[2])
            ? name.Substring(2)
            : null;
        if (eventName is not null && control.Events.Any(candidate => candidate.Name == eventName)) {
            var component = FindComponent(analysis, compilation, cancellationToken);
            return component is null
                ? Array.Empty<MarkupCompletionItem>()
                : CompleteMembers(component, cancellationToken)
                    .Where(static item => item.Kind is MarkupCompletionKind.Method or
                        MarkupCompletionKind.Property or MarkupCompletionKind.Field)
                    .ToArray();
        }

        if (schema is null) {
            return Array.Empty<MarkupCompletionItem>();
        }

        if (schema.ValueKind == "Boolean") {
            return
            [
                new MarkupCompletionItem("true", "Boolean", "true", MarkupCompletionKind.Value),
                new MarkupCompletionItem("false", "Boolean", "false", MarkupCompletionKind.Value),
            ];
        }

        return schema.AllowedValues
            .Select(value => new MarkupCompletionItem(
                value,
                schema.TypeName ?? "Enum value",
                value,
                MarkupCompletionKind.Value))
            .ToArray();
    }

    private static IReadOnlyList<MarkupCompletionItem> CompleteAttributes(
        string source,
        MarkupControlInfo control,
        IReadOnlyList<AttributeSpan> present,
        bool isRoot) {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in present) {
            if (attribute.NameEnd > attribute.NameStart) {
                existing.Add(source.Substring(
                    attribute.NameStart,
                    attribute.NameEnd - attribute.NameStart));
            }
        }

        var items = new List<MarkupCompletionItem>();
        if (isRoot) {
            items.AddRange(MetadataCompletions());
        }

        foreach (var attribute in control.Attributes.Where(item => !existing.Contains(item.Name))) {
            items.Add(new MarkupCompletionItem(
                attribute.Name,
                attribute.TypeName ?? attribute.ValueKind,
                attribute.Name,
                MarkupCompletionKind.Property,
                Definition: attribute.Definition));
        }

        foreach (var markupEvent in control.Events) {
            var name = "on" + markupEvent.Name;
            if (existing.Contains(name)) {
                continue;
            }

            items.Add(new MarkupCompletionItem(
                name,
                EventDetail(markupEvent),
                name + "=\"\"",
                MarkupCompletionKind.Event,
                Documentation: EventDetail(markupEvent)));
        }

        return items;
    }

    private static IReadOnlyList<MarkupCompletionItem> CompleteMarkupItems(
        MarkupAnalysis analysis,
        ControlCatalog controls) {
        return CompleteControls(analysis, controls)
            .Concat(DirectiveCompletions())
            .Concat(MetadataCompletions())
            .ToArray();
    }

    private static IReadOnlyList<MarkupCompletionItem> CompleteControls(
        MarkupAnalysis analysis,
        ControlCatalog controls) {
        var packageControls = string.IsNullOrWhiteSpace(analysis.Namespace)
            ? Array.Empty<ControlDefinition>()
            : controls.All
                .Where(control => control.Namespace == analysis.Namespace)
                .ToArray();
        var packageNames = new HashSet<string>(
            packageControls.Select(static control => control.Name),
            StringComparer.Ordinal);
        var uniqueGlobal = controls.All
            .Where(control => !packageNames.Contains(control.Name))
            .GroupBy(static control => control.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() == 1)
            .Select(static group => group.Single());

        return packageControls
            .Concat(uniqueGlobal)
            .OrderBy(control => control.Name, StringComparer.Ordinal)
            .Select(control => new MarkupCompletionItem(
                control.Name,
                control.QualifiedName,
                control.Name,
                MarkupCompletionKind.Class,
                Documentation: control.IsComponent
                    ? "Melanopsin component"
                    : "Melanopsin markup element",
                Definition: new MarkupDefinitionTarget(control.TypeName)))
            .ToArray();
    }

    private static IReadOnlyList<MarkupCompletionItem> CompleteMembers(
        INamedTypeSymbol component,
        CancellationToken cancellationToken,
        bool includePrivate = false) {
        var items = new List<MarkupCompletionItem>();
        foreach (var symbol in EnumerateMembers(component, includePrivate)) {
            cancellationToken.ThrowIfCancellationRequested();
            var target = new MarkupDefinitionTarget(
                component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                symbol.Name);
            switch (symbol) {
                case IPropertySymbol property:
                    items.Add(new MarkupCompletionItem(
                        property.Name,
                        property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        property.Name,
                        MarkupCompletionKind.Property,
                        Definition: target));
                    break;
                case IFieldSymbol field:
                    items.Add(new MarkupCompletionItem(
                        field.Name,
                        field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        field.Name,
                        MarkupCompletionKind.Field,
                        Definition: target));
                    break;
                case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                    items.Add(new MarkupCompletionItem(
                        method.Name,
                        method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        method.Name + "($0)",
                        MarkupCompletionKind.Method,
                        IsSnippet: true,
                        Definition: target));
                    break;
                case IEventSymbol markupEvent:
                    items.Add(new MarkupCompletionItem(
                        markupEvent.Name,
                        markupEvent.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        markupEvent.Name,
                        MarkupCompletionKind.Event,
                        Definition: target));
                    break;
            }
        }

        return items
            .GroupBy(static item => item.Label, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static item => item.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static MarkupControlInfo? FindControl(
        MarkupAnalysis analysis,
        ControlCatalog controls,
        string tagName) {
        var name = tagName.Substring(tagName.LastIndexOf(':') + 1);
        var parsed = analysis.Elements
            .SelectMany(Flatten)
            .FirstOrDefault(element => element.QualifiedName == tagName);
        if (parsed is not null) {
            return parsed.Control;
        }

        var package = analysis.Namespace;
        var control = package is { Length: > 0 }
            ? controls.Find(package, name)
            : null;
        if (control is null) {
            var candidates = controls.FindAll(name);
            control = candidates.Count == 1 ? candidates[0] : null;
        }

        return control is null ? null : ToInfo(control);
    }

    private static IReadOnlyList<MarkupCompletionItem> DirectiveCompletions() {
        return
        [
            Directive("@if", "@if (${1:condition}) {\n    $0\n}", "Conditional markup block"),
            Directive("@else", "@else {\n    $0\n}", "Fallback conditional block"),
            Directive("@for", "@for (var ${1:item} in ${2:Items}) {\n    $0\n}", "Repeated markup block"),
            Directive("@switch", "@switch (${1:value}) {\n    @case ${2:Value} {\n        $0\n    }\n}", "Switch markup block"),
            Directive("@case", "@case ${1:Value} {\n    $0\n}", "Switch case block"),
            Directive(
                "@code",
                "@code {\n    $0\n}",
                "C# members — fields, properties and methods — compiled into the component class"),
        ];
    }

    private static MarkupCompletionItem Directive(
        string label,
        string insertText,
        string documentation) {
        return new MarkupCompletionItem(
            label,
            "mxml directive",
            insertText,
            MarkupCompletionKind.Keyword,
            IsSnippet: true,
            Documentation: documentation);
    }

    private static IReadOnlyList<MarkupCompletionItem> MetadataCompletions() {
        return
        [
            new("package", "C# namespace", "package=\"\"", MarkupCompletionKind.Property),
            new("component", "C# partial class", "component=\"\"", MarkupCompletionKind.Property),
            new("resources", "Resource package", "resources=\"\"", MarkupCompletionKind.Property),
        ];
    }
}
