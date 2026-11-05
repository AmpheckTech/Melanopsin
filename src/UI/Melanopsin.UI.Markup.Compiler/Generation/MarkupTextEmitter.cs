using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;
using Melanopsin.UI.Markup.Compiler.Generation.Values;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal sealed class MarkupTextEmitter {
    private readonly MarkupNodeEmitter _nodes;

    public MarkupTextEmitter(MarkupNodeEmitter nodes) {
        _nodes = nodes;
    }

    internal bool TryEmit(
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

        var values = new List<CodeFragment>(textNodes.Length);
        foreach (var child in textNodes) {
            values.Add(child switch {
                MarkupText text => CodeFragment.Text(CSharpLiteral.Quote(text.Text)),
                MarkupExpression expression =>
                    MarkupValueEmitter.ToStringExpression(
                        CodeFragment.Source(
                            expression.Expression,
                            expression.ExpressionOffset)),
                _ => throw new InvalidOperationException(
                    $"Unsupported text node '{child.GetType().Name}'."),
            });
        }

        var valueExpression = CodeFragment.Empty;
        if (values.Count == 1) {
            valueExpression.Add(values[0]);
        } else {
            valueExpression.Add("global::System.String.Concat(");
            for (var index = 0; index < values.Count; index++) {
                if (index != 0) {
                    valueExpression.Add(", ");
                }

                valueExpression.Add(values[index]);
            }

            valueExpression.Add(")");
        }

        _nodes.AppendLine(indent, $"{variable}.Bind(");
        _nodes.AppendLine(indent + 1, CodeFragment.Text("() => ").Add(valueExpression).Add(","));
        _nodes.AppendLine(
            indent + 1,
            "static (__target, __value) => __target.SetTextContent(__value));");
        return true;
    }
}
