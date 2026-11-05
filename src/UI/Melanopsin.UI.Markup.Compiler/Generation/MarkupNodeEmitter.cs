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
    private readonly MarkupControlResolver _resolver;
    private readonly MarkupElementEmitter _elements;
    private readonly MarkupControlFlowEmitter _controlFlow;
    private readonly MarkupResourceResolver _resources;
    private readonly string _source;
    private readonly string _path;
    private readonly HashSet<string> _referenceNames = new(StringComparer.Ordinal);
    private readonly List<MarkupReferenceBinding> _references = [];
    private int _variableIndex;

    public MarkupNodeEmitter(
        StringBuilder output,
        MarkupControlResolver resolver,
        string? package,
        ResourceManifestCatalog manifests,
        string source,
        string path) {
        _output = output;
        _resolver = resolver;
        _source = source;
        _path = path;
        Package = package;
        _resources = new MarkupResourceResolver(manifests);
        _elements = new MarkupElementEmitter(this);
        _controlFlow = new MarkupControlFlowEmitter(this);
    }

    internal MarkupControlResolver Resolver => _resolver;

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
                    MarkupValueEmitter.ToStringExpression(
                        CodeFragment.Source(expression.Expression, expression.ExpressionOffset)));
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

        _references.Add(new MarkupReferenceBinding(reference.Name, control.ElementTypeName));
        AppendLine(indent, $"{reference.Name} = {variable};");
    }

    internal void AppendLine(int indent, string value) {
        _output.Append(' ', indent * 4).AppendLine(value);
    }

    /// <summary>
    /// Writes a line of generated code. A fragment copied from the document goes under its own
    /// <c>#line</c> span, so the compiler maps its columns back onto the markup.
    /// </summary>
    internal void AppendLine(int indent, CodeFragment fragment) {
        _output.Append(' ', indent * 4);
        foreach (var part in fragment.Parts) {
            if (part.IsSource &&
                TryLocate(part, out var line, out var column, out var endLine, out var endColumn)) {
                _output.AppendLine()
                    .Append("#line (").Append(line).Append(',').Append(column)
                    .Append(")-(").Append(endLine).Append(',').Append(endColumn)
                    .Append(") \"").Append(_path.Replace("\\", "\\\\")).AppendLine("\"")
                    .Append(part.Text);
                if (!part.Text.EndsWith("\n", StringComparison.Ordinal)) {
                    _output.AppendLine();
                }

                _output.AppendLine("#line default");
                continue;
            }

            _output.Append(part.Text);
        }

        _output.AppendLine();
    }

    /// <summary>
    /// Where a copied piece of code starts in the document. Entities and line breaks are rewritten
    /// by the XML reader, so a piece that no longer matches the text it came from is emitted without
    /// a directive rather than pointed at the wrong place.
    /// </summary>
    private bool TryLocate(
        CodeFragment.Part part,
        out int line,
        out int column,
        out int endLine,
        out int endColumn) {
        line = 0;
        column = 0;
        endLine = 0;
        endColumn = 0;
        var end = part.Offset + part.Text.Length;
        if (end > _source.Length ||
            !MarkupEventBinding.TextMatches(
                part.Text,
                _source.Substring(part.Offset, part.Text.Length))) {
            return false;
        }

        line = 1;
        column = 1;
        for (var index = 0; index < part.Offset; index++) {
            if (_source[index] == '\n') {
                line++;
                column = 1;
            } else {
                column++;
            }
        }

        endLine = line;
        endColumn = column;
        for (var index = part.Offset; index < end; index++) {
            if (_source[index] == '\n') {
                endLine++;
                endColumn = 1;
            } else {
                endColumn++;
            }
        }

        return true;
    }

    private void AppendText(int indent, string? parentVariable, string valueExpression) {
        AppendText(indent, parentVariable, CodeFragment.Text(valueExpression));
    }

    private void AppendText(int indent, string? parentVariable, CodeFragment valueExpression) {
        if (parentVariable is null) {
            throw new InvalidOperationException("Text can only be emitted inside an element.");
        }

        AppendLine(
            indent,
            CodeFragment.Text($"{parentVariable}.AppendContent(")
                .Add(valueExpression)
                .Add(");"));
    }
}
