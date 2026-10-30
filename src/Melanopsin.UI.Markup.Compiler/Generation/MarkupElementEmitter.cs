using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;
using Melanopsin.UI.Markup.Compiler.Generation.Values;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal sealed class MarkupElementEmitter {
    private readonly MarkupNodeEmitter _nodes;

    public MarkupElementEmitter(MarkupNodeEmitter nodes) {
        _nodes = nodes;
    }

    internal string Emit(
        MarkupElement element,
        int indent,
        string? parentVariable,
        string? reconciliationKey) {
        var control = _nodes.Controls.Find(element.Name) ??
                      throw new InvalidOperationException(
                          $"Markup element '{element.Name}' has no registered control type.");
        var variable = _nodes.NextVariable("__melanopsinElement");
        _nodes.AppendLine(indent, $"var {variable} = new {control.TypeName}();");
        _nodes.AppendLine(
            indent + 1,
            $"{variable}.ReconciliationSlot = {element.Offset};");
        if (reconciliationKey is { Length: > 0 }) {
            _nodes.AppendLine(
                indent + 1,
                $"{variable}.ReconciliationKey = (object?)" +
                $"({reconciliationKey});");
        }

        foreach (var attribute in element.Attributes) {
            if (MarkupSchema.IsReferenceAttribute(attribute)) {
                if (!MarkupReference.TryParse(attribute, out var reference)) {
                    throw new InvalidOperationException(
                        $"Reference on '{element.Name}' was not validated.");
                }

                _nodes.RegisterReference(
                    reference,
                    control,
                    variable,
                    indent + 1);
                continue;
            }

            var definition = control.Attributes[attribute.Name];
            if (TryEmitResourceBinding(
                    control,
                    variable,
                    attribute,
                    definition,
                    indent + 1)) {
                continue;
            }

            if (TryEmitInlineLength(variable, attribute, indent + 1)) {
                continue;
            }

            if (attribute.Parts.Any(static part => part.IsExpression)) {
                EmitAttributeBinding(
                    control,
                    variable,
                    attribute,
                    definition,
                    indent + 1);
                continue;
            }

            _nodes.AppendLine(
                indent + 1,
                $"{variable}.SetAttributeValue(" +
                $"{CSharpLiteral.Quote(attribute.Name)}, " +
                $"{MarkupValueEmitter.AttributeValue(attribute.Parts)});");
            if (definition.PropertyName is { } propertyName) {
                _nodes.AppendLine(
                    indent + 1,
                    $"{variable}.{propertyName} = " +
                    $"{MarkupValueEmitter.PropertyValue(attribute.Parts, definition)};");
            }
        }

        var hasTextBinding = EmitTextBinding(
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

    private void EmitAttributeBinding(
        ControlDefinition control,
        string variable,
        MarkupAttribute attribute,
        MarkupAttributeDefinition definition,
        int indent) {
        var attributeValue = MarkupValueEmitter.AttributeValue(attribute.Parts);
        _nodes.AppendLine(indent, $"{variable}.Bind(");
        _nodes.AppendLine(indent + 1, $"() => {attributeValue},");
        _nodes.AppendLine(indent + 1, "static (__target, __value) => {");
        _nodes.AppendLine(
            indent + 2,
            $"__target.SetAttributeValue({CSharpLiteral.Quote(attribute.Name)}, __value);");
        if (definition.PropertyName is { } propertyName) {
            var propertyValue = MarkupValueEmitter.BoundPropertyValue(
                "__value",
                attribute.Parts.Count,
                definition);
            _nodes.AppendLine(
                indent + 2,
                $"(({control.TypeName})__target).{propertyName} = {propertyValue};");
        }

        _nodes.AppendLine(indent + 1, "});");
    }

    private bool TryEmitResourceBinding(
        ControlDefinition control,
        string variable,
        MarkupAttribute attribute,
        MarkupAttributeDefinition definition,
        int indent) {
        var resourcePart = attribute.Parts.FirstOrDefault(
            static part => part.IsResource);
        if (resourcePart is null) {
            return false;
        }

        if (attribute.Parts.Count != 1 ||
            !ResourceReference.TryParse(resourcePart.Value, out var reference) ||
            !_nodes.Resources.TryResolve(
                _nodes.Package,
                reference,
                out var resource)) {
            throw new InvalidOperationException(
                $"Resource attribute '{attribute.Name}' was not validated.");
        }

        if (InlineLengthProperty.TryGet(attribute.Name, out var lengthProperty)) {
            _nodes.AppendLine(indent, $"{variable}.Bind(");
            _nodes.AppendLine(
                indent + 1,
                $"() => GetResources().Get({resource.KeyExpression}),");
            _nodes.AppendLine(
                indent + 1,
                $"static (__target, __value) => " +
                $"__target.InlineStyle.{lengthProperty} = __value);");
            return true;
        }

        _nodes.AppendLine(indent, $"{variable}.Bind(");
        _nodes.AppendLine(
            indent + 1,
            $"() => GetResources().Get({resource.KeyExpression}),");
        _nodes.AppendLine(indent + 1, "static (__target, __value) => {");
        _nodes.AppendLine(
            indent + 2,
            $"__target.SetAttributeValue(" +
            $"{CSharpLiteral.Quote(attribute.Name)}, __value);");
        if (definition.PropertyName is { } propertyName) {
            _nodes.AppendLine(
                indent + 2,
                $"(({control.TypeName})__target).{propertyName} = __value;");
        }

        _nodes.AppendLine(indent + 1, "});");
        return true;
    }

    private bool EmitTextBinding(
        string variable,
        IReadOnlyList<MarkupNode> children,
        int indent) {
        if (children.Any(static child => child is not MarkupText and not MarkupExpression)) {
            return false;
        }

        var textNodes = children
            .Where(static child => child is MarkupText or MarkupExpression)
            .ToArray();
        if (!textNodes.Any(static child => child is MarkupExpression)) {
            return false;
        }

        var values = new List<string>(textNodes.Length);
        foreach (var child in textNodes) {
            values.Add(child switch {
                MarkupText text => CSharpLiteral.Quote(text.Text),
                MarkupExpression expression =>
                    MarkupValueEmitter.ToStringExpression(expression.Expression),
                _ => throw new InvalidOperationException(
                    $"Unsupported text node '{child.GetType().Name}'."),
            });
        }

        var valueExpression = values.Count == 1
            ? values[0]
            : $"global::System.String.Concat({string.Join(", ", values)})";
        _nodes.AppendLine(indent, $"{variable}.Bind(");
        _nodes.AppendLine(indent + 1, $"() => {valueExpression},");
        _nodes.AppendLine(
            indent + 1,
            "static (__target, __value) => __target.SetTextContent(__value));");
        return true;
    }

    private bool TryEmitInlineLength(
        string variable,
        MarkupAttribute attribute,
        int indent) {
        if (!InlineLengthProperty.TryGet(attribute.Name, out var property)) {
            return false;
        }

        if (attribute.Parts.Any(static part => part.IsExpression)) {
            _nodes.AppendLine(indent, $"{variable}.Bind(");
            _nodes.AppendLine(
                indent + 1,
                $"() => {MarkupValueEmitter.LengthExpression(attribute.Parts)},");
            _nodes.AppendLine(
                indent + 1,
                $"static (__target, __value) => __target.InlineStyle.{property} = __value);");
            return true;
        }

        _nodes.AppendLine(
            indent,
            $"{variable}.InlineStyle.{property} = " +
            $"{MarkupValueEmitter.LengthExpression(attribute.Parts)};");
        return true;
    }
}
