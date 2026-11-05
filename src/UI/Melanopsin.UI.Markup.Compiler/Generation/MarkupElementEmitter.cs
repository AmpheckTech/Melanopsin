using System;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal sealed class MarkupElementEmitter {
    private readonly MarkupNodeEmitter _nodes;
    private readonly MarkupAttributeEmitter _attributes;
    private readonly MarkupTextEmitter _text;

    public MarkupElementEmitter(MarkupNodeEmitter nodes) {
        _nodes = nodes;
        _attributes = new MarkupAttributeEmitter(nodes);
        _text = new MarkupTextEmitter(nodes);
    }

    internal string Emit(
        MarkupElement element,
        int indent,
        string? parentVariable,
        string? reconciliationKey) {
        var control = _nodes.Resolver.Find(element) ??
                      throw new InvalidOperationException(
                          $"Markup element '{element.QualifiedName}' has no registered control type.");
        var variable = _nodes.NextVariable("__melanopsinElement");
        if (control.IsComponent) {
            var component = _nodes.NextVariable("__melanopsinComponent");
            _nodes.AppendLine(indent, $"var {component} = new {control.TypeName}();");
            _nodes.AppendLine(
                indent,
                $"var {variable} = new global::Melanopsin.UI.ComponentHost(" +
                $"{component}, Runtime);");
        } else {
            _nodes.AppendLine(indent, $"var {variable} = new {control.TypeName}();");
        }
        _nodes.AppendLine(
            indent + 1,
            $"{variable}.ReconciliationSlot = {element.Offset};");
        if (reconciliationKey is { Length: > 0 }) {
            _nodes.AppendLine(
                indent + 1,
                $"{variable}.ReconciliationKey = (object?)" +
                $"({reconciliationKey});");
        }

        _attributes.Emit(
            control,
            element.Name,
            variable,
            element.Attributes,
            indent + 1);

        var hasTextBinding = _text.TryEmit(
            variable,
            element.Children,
            indent + 1);
        foreach (var child in element.Children) {
            if (hasTextBinding && child is MarkupText or MarkupExpression) {
                continue;
            }

            _nodes.Emit(
                child,
                indent + 1,
                variable,
                reconciliationKey: null);
        }

        if (parentVariable is not null) {
            _nodes.AppendLine(indent, $"{parentVariable}.AppendChild({variable});");
        }

        return variable;
    }
}
