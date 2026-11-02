using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class MarkupLanguageService {
    private static MarkupHoverInfo? GetHover(
        string source,
        int offset,
        MarkupAnalysis analysis,
        Compilation compilation,
        CancellationToken cancellationToken) {
        var component = FindComponent(analysis, compilation, cancellationToken);
        if (InsideCodeBlock(analysis, offset)) {
            // A @code block is plain C#, not markup: only component members resolve here, and
            // unlike markup it may reach private ones.
            return component is null
                ? null
                : HoverMember(source, offset, component, includePrivate: true);
        }

        var interpolationStart = FindInterpolationStart(source, offset);
        if (component is not null && interpolationStart >= 0) {
            return HoverMember(source, offset, component);
        }

        if (!TryFindOpenTag(source, offset, out var tag)) {
            return null;
        }

        var element = FindInnermostElement(analysis.Elements, source, offset);
        var control = element?.Control;
        if (offset <= tag.NameEnd) {
            return control is null
                ? null
                : new MarkupHoverInfo(
                    ControlMarkdown(control),
                    tag.NameStart,
                    tag.NameEnd - tag.NameStart,
                    control.Definition);
        }

        var attribute = tag.Attributes.FirstOrDefault(candidate =>
            Contains(candidate.NameStart, candidate.NameEnd - candidate.NameStart, offset) ||
            candidate.ValueStart >= 0 &&
            Contains(candidate.ValueStart, candidate.ValueEnd - candidate.ValueStart, offset));
        if (attribute.NameEnd <= attribute.NameStart) {
            return null;
        }

        var attributeName = source.Substring(
            attribute.NameStart,
            attribute.NameEnd - attribute.NameStart);
        if (attribute.ValueStart >= 0 &&
            offset >= attribute.ValueStart &&
            component is not null) {
            var hover = HoverMember(source, offset, component);
            if (hover is not null) {
                return hover;
            }
        }

        if (control is null) {
            return HoverMetadata(attributeName, attribute, analysis);
        }

        var eventName = EventNameOf(attributeName);
        var markupEvent = eventName is null
            ? null
            : control.Events.FirstOrDefault(candidate => candidate.Name == eventName);
        if (markupEvent is not null) {
            return new MarkupHoverInfo(
                $"**{control.Name}.{attributeName}**\n\nEvent `{markupEvent.DelegateTypeName}`  \n" +
                $"Handler arguments: `{EventDetail(markupEvent)}`",
                attribute.NameStart,
                attribute.NameEnd - attribute.NameStart,
                new MarkupDefinitionTarget(control.TypeName, markupEvent.Name));
        }

        var schema = control.Attributes.FirstOrDefault(candidate =>
            candidate.Name == attributeName);
        if (schema is null) {
            return HoverMetadata(attributeName, attribute, analysis);
        }

        var values = schema.AllowedValues.Count == 0
            ? string.Empty
            : $"\n\nValues: {string.Join(", ", schema.AllowedValues.Select(static value => $"`{value}`"))}";
        return new MarkupHoverInfo(
            $"**{control.Name}.{schema.Name}**\n\nType `{schema.TypeName ?? schema.ValueKind}`" +
            $"  \nMarkup value: `{schema.ValueKind}`{values}",
            attribute.NameStart,
            attribute.NameEnd - attribute.NameStart,
            schema.Definition);
    }

    private static bool InsideCodeBlock(MarkupAnalysis analysis, int offset) {
        foreach (var block in analysis.CodeBlocks) {
            if (offset >= block.Offset && offset < block.Offset + block.Length) {
                return true;
            }
        }

        return false;
    }

    private static MarkupHoverInfo? HoverMember(
        string source,
        int offset,
        INamedTypeSymbol component,
        bool includePrivate = false) {
        var (start, end) = IdentifierAt(source, offset);
        if (end <= start) {
            return null;
        }

        var name = source.Substring(start, end - start);
        var member = EnumerateMembers(component, includePrivate)
            .FirstOrDefault(candidate => candidate.Name == name);
        if (member is null) {
            return null;
        }

        var signature = member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var containingType = member.ContainingType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
        return new MarkupHoverInfo(
            $"**{member.Name}**\n\n`{signature}`  \nDeclared in `{containingType}`",
            start,
            end - start,
            new MarkupDefinitionTarget(containingType, member.Name));
    }

    private static MarkupHoverInfo HoverMetadata(
        string name,
        AttributeSpan attribute,
        MarkupAnalysis analysis) {
        var description = name switch {
            "package" => "C# namespace that owns the generated component partial class.",
            "component" => "Name of the partial Component class that receives the generated markup.",
            "resources" => "Resource package used by `{res Category.Name}` references.",
            "class" => "CSS classes applied to this element.",
            _ => "Markup attribute.",
        };
        var definition = name switch {
            "component" => analysis.Component,
            "package" => analysis.Namespace is { Length: > 0 } package
                ? new MarkupDefinitionTarget(package)
                : null,
            _ => null,
        };
        return new MarkupHoverInfo(
            $"**{name}**\n\n{description}",
            attribute.NameStart,
            attribute.NameEnd - attribute.NameStart,
            definition);
    }

    private static string ControlMarkdown(MarkupControlInfo control) {
        var kind = control.IsComponent ? "component" : "markup element";
        return $"**{control.Name}** — {kind}\n\n`{control.TypeName}`  \n" +
               $"Namespace `{control.Namespace}` · assembly `{control.AssemblyName}`";
    }
}
