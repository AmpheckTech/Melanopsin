using System;
using System.Collections.Generic;
using System.Text;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;
using Melanopsin.UI.Markup.Compiler.Generation.Values;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal sealed class MarkupNodeEmitter {
    private readonly StringBuilder _output;
    private readonly ControlCatalog _controls;
    private readonly MarkupElementEmitter _elements;
    private readonly MarkupControlFlowEmitter _controlFlow;
    private readonly MarkupResourceResolver _resources;
    private readonly HashSet<string> _referenceNames = new(StringComparer.Ordinal);
    private readonly List<MarkupReferenceBinding> _references = [];
    private int _variableIndex;

    public MarkupNodeEmitter(
        StringBuilder output,
        ControlCatalog controls,
        string? package,
        ResourceManifestCatalog manifests) {
        _output = output;
        _controls = controls;
        Package = package;
        _resources = new MarkupResourceResolver(manifests);
        _elements = new MarkupElementEmitter(this);
        _controlFlow = new MarkupControlFlowEmitter(this);
    }

    internal ControlCatalog Controls => _controls;

    internal string? Package { get; }

    internal MarkupResourceResolver Resources => _resources;

    internal IReadOnlyList<MarkupReferenceBinding> References => _references;

    internal string? Emit(
        MarkupNode node,
        int indent,
        string? parentVariable,
        string? reconciliationKey) {
        switch (node) {
            case MarkupElement element:
                return _elements.Emit(
                    element,
                    indent,
                    parentVariable,
                    reconciliationKey);

            case MarkupText text:
                if (!string.IsNullOrWhiteSpace(text.Text)) {
                    AppendText(indent, parentVariable, CSharpLiteral.Quote(text.Text));
                }

                return null;

            case MarkupExpression expression:
                AppendText(
                    indent,
                    parentVariable,
                    MarkupValueEmitter.ToStringExpression(expression.Expression));
                return null;

            case MarkupIf conditional:
                _controlFlow.EmitIf(
                    conditional,
                    indent,
                    parentVariable,
                    reconciliationKey);
                return null;

            case MarkupFor loop:
                _controlFlow.EmitFor(loop, indent, parentVariable);
                return null;

            case MarkupSwitch switchBlock:
                _controlFlow.EmitSwitch(switchBlock, indent, parentVariable);
                return null;

            default:
                throw new InvalidOperationException($"Unsupported markup node '{node.GetType().Name}'.");
        }
    }

    internal string NextVariable(string prefix) {
        return $"{prefix}{_variableIndex++}";
    }

    internal void RegisterReference(
        MarkupReference reference,
        ControlDefinition control,
        string variable,
        int indent) {
        if (!_referenceNames.Add(reference.Name)) {
            throw new InvalidOperationException(
                $"Markup reference '{reference.Name}' is declared more than once.");
        }

        _references.Add(new MarkupReferenceBinding(reference.Name, control.TypeName));
        AppendLine(indent, $"{reference.Name} = {variable};");
    }

    internal void AppendLine(int indent, string value) {
        _output.Append(' ', indent * 4).AppendLine(value);
    }

    private void AppendText(int indent, string? parentVariable, string valueExpression) {
        if (parentVariable is null) {
            throw new InvalidOperationException("Text can only be emitted inside an element.");
        }

        AppendLine(indent, $"{parentVariable}.AppendContent({valueExpression});");
    }
}
