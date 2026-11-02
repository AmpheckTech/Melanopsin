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

internal sealed class MarkupAttributeEmitter {
    private readonly MarkupNodeEmitter _nodes;

    public MarkupAttributeEmitter(MarkupNodeEmitter nodes) {
        _nodes = nodes;
    }

    internal void Emit(
        ControlDefinition control,
        string elementName,
        string variable,
        IReadOnlyList<MarkupAttribute> attributes,
        int indent) {
        foreach (var attribute in attributes) {
            if (MarkupSchema.IsReferenceAttribute(attribute)) {
                if (!MarkupReference.TryParse(attribute, out var reference)) {
                    throw new InvalidOperationException(
                        $"Reference on '{elementName}' was not validated.");
                }

                _nodes.RegisterReference(
                    reference,
                    control,
                    variable,
                    indent);
                continue;
            }

            if (MarkupEventBinding.TryGetEventName(attribute, out var eventName)) {
                EmitEventBinding(
                    control,
                    variable,
                    attribute,
                    eventName,
                    indent);
                continue;
            }

            var definition = control.Attributes[attribute.Name];
            if (TryEmitResourceBinding(
                    control,
                    variable,
                    attribute,
                    definition,
                    indent)) {
                continue;
            }

            if (TryEmitInlineLength(variable, attribute, indent)) {
                continue;
            }

            if (attribute.Parts.Any(static part => part.IsExpression)) {
                EmitAttributeBinding(
                    variable,
                    attribute,
                    definition,
                    indent);
                continue;
            }

            if (definition.DescriptorAccessor is { } descriptor) {
                _nodes.AppendLine(
                    indent,
                    CodeFragment.Text($"{variable}.SetAttributeValue(")
                        .Add(descriptor)
                        .Add(", ")
                        .Add(MarkupValueEmitter.AttributeValue(attribute.Parts, definition))
                        .Add(");"));
            } else {
                _nodes.AppendLine(
                    indent,
                    CodeFragment.Text($"{variable}.SetAttributeValue(")
                        .Add(CSharpLiteral.Quote(attribute.Name))
                        .Add(", ")
                        .Add(MarkupValueEmitter.AttributeValue(attribute.Parts, definition))
                        .Add(");"));
                if (definition.PropertyName is { } propertyName) {
                    _nodes.AppendLine(
                        indent,
                        CodeFragment.Text($"{variable}.{propertyName} = ")
                            .Add(MarkupValueEmitter.PropertyValue(attribute.Parts, definition))
                            .Add(";"));
                }
            }
        }
    }

    private void EmitEventBinding(
        ControlDefinition control,
        string variable,
        MarkupAttribute attribute,
        string eventName,
        int indent) {
        if (!control.Events.TryGetValue(eventName, out var eventDefinition) ||
            !MarkupEventBinding.TryGetStatement(attribute, out var statement)) {
            throw new InvalidOperationException(
                $"Event attribute '{attribute.Name}' was not validated.");
        }

        var handlerVariable = _nodes.NextVariable("__melanopsinEventHandler");
        var rewritten = MarkupEventBinding.RewriteEventArgument(statement.Trim(), out _);
        var lambdaParameters = eventDefinition.ParameterCount == 1
            ? MarkupEventBinding.EmittedParameterName
            : string.Empty;
        _nodes.AppendLine(
            indent,
            $"{eventDefinition.DelegateTypeName} {handlerVariable} = " +
            $"({lambdaParameters}) => {{");
        var eventStatement = rewritten.Trim();
        // The handler is copied straight out of the markup, so a #line can point the compiler at
        // the exact column the author wrote it in.
        var eventOffset = attribute.Parts[0].Offset + LeadingWhitespace(attribute.Parts[0].Value);
        var eventFragment = CodeFragment.Source(eventStatement, eventOffset);
        if (!eventStatement.EndsWith(";", StringComparison.Ordinal)) {
            eventFragment.Add(";");
        }

        _nodes.AppendLine(indent + 1, eventFragment);

        _nodes.AppendLine(indent + 1, "Invalidate();");
        _nodes.AppendLine(indent, "};");
        _nodes.AppendLine(indent, $"{variable}.BindEvent(");
        _nodes.AppendLine(
            indent + 1,
            $"__target => (({control.TypeName})__target).{eventName} += {handlerVariable},");
        _nodes.AppendLine(
            indent + 1,
            $"__target => (({control.TypeName})__target).{eventName} -= {handlerVariable});");
    }

    private void EmitAttributeBinding(
        string variable,
        MarkupAttribute attribute,
        MarkupAttributeDefinition definition,
        int indent) {
        var attributeValue = definition.ValueKind == SchemaValueKind.Length
            ? MarkupValueEmitter.LengthExpression(attribute.Parts)
            : MarkupValueEmitter.AttributeValue(attribute.Parts);
        var bindingTarget = definition.DescriptorAccessor is { } descriptor
            ? descriptor + ","
            : CSharpLiteral.Quote(attribute.Name) + ",";
        _nodes.AppendLine(indent, $"{variable}.BindAttribute(");
        _nodes.AppendLine(indent + 1, bindingTarget);
        _nodes.AppendLine(indent + 1, CodeFragment.Text("() => ").Add(attributeValue).Add(");"));
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

        if (definition.DescriptorAccessor is { } descriptor) {
            _nodes.AppendLine(indent, $"{variable}.Bind(");
            _nodes.AppendLine(
                indent + 1,
                $"() => GetResources().Get({resource.KeyExpression}),");
            _nodes.AppendLine(
                indent + 1,
                $"static (__target, __value) => __target.SetAttributeValue({descriptor}, __value));");
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

    private bool TryEmitInlineLength(
        string variable,
        MarkupAttribute attribute,
        int indent) {
        if (!InlineLengthProperty.TryGet(attribute.Name, out var property)) {
            return false;
        }

        if (attribute.Parts.Any(static part => part.IsExpression)) {
            _nodes.AppendLine(indent, $"{variable}.BindAttribute(");
            _nodes.AppendLine(
                indent + 1,
                $"{CSharpLiteral.Quote(attribute.Name)},");
            _nodes.AppendLine(
                indent + 1,
                CodeFragment.Text("() => ")
                    .Add(MarkupValueEmitter.LengthExpression(attribute.Parts))
                    .Add(");"));
            return true;
        }

        _nodes.AppendLine(
            indent,
            CodeFragment.Text($"{variable}.InlineStyle.{property} = ")
                .Add(MarkupValueEmitter.LengthExpression(attribute.Parts))
                .Add(";"));
        return true;
    }

    private static int LeadingWhitespace(string value) {
        var count = 0;
        while (count < value.Length && char.IsWhiteSpace(value[count])) {
            count++;
        }

        return count;
    }
}
