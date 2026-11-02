using System;
using System.Collections.Generic;
using System.Text;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssRuleAssignment {
    public CssRuleAssignment(string selectorExpression, string properties) {
        SelectorExpression = selectorExpression;
        Properties = properties;
    }

    public string SelectorExpression { get; }

    public string Properties { get; }
}

internal static class CssRuleEmitter {
    public static CssRuleAssignment? Emit(
        CssRule rule,
        IReadOnlyDictionary<string, CssTokenState> states,
        List<CssDiagnostic> diagnostics) {
        CssPseudoElementValidator.Validate(rule, diagnostics);
        var properties = new StringBuilder();
        var declaredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in rule.Declarations) {
            if (declaration.Name.StartsWith("$", StringComparison.Ordinal)) {
                continue;
            }

            if (!CssValueSyntax.TryGetProperty(declaration.Name, out var property)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Unknown style property '{declaration.Name}'.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            if (!declaredProperties.Add(declaration.Name)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Style property '{declaration.Name}' is declared more than once in selector '{rule.Selector}'. Use one typed declaration per property.",
                    declaration.Offset,
                    declaration.Length));
                continue;
            }

            var expression = CssTokenResolver.ResolveValue(
                declaration.Value,
                property.Kind,
                declaration.ValueOffset,
                states,
                diagnostics);
            if (expression is null) {
                continue;
            }

            properties.Append("                    ")
                .Append(property.Member)
                .Append(" = ")
                .Append(expression)
                .AppendLine(",");
        }

        return properties.Length == 0
            ? null
            : new CssRuleAssignment(
                SelectorExpression(rule.Selector),
                properties.ToString());
    }

    private static string SelectorExpression(CssComplexSelector selector) {
        var expression = CompoundExpression(selector.Rightmost, includePseudoElement: false);
        if (selector.Parent is not null) {
            var method = selector.Combinator == CssCombinator.Child
                ? "ChildOf"
                : "DescendantOf";
            expression += "." + method + "(" + SelectorExpression(selector.Parent) + ")";
        }

        return AppendPseudoElement(expression, selector.Rightmost);
    }

    private static string CompoundExpression(
        CssCompoundSelector compound,
        bool includePseudoElement) {
        const string prefix = "global::Melanopsin.UI.Styling.StyleSelector.";
        var expression = compound.TypeName is not null
            ? prefix + "Type(" + CSharpLiteral.Quote(compound.TypeName) + ")"
            : prefix + "Universal";
        if (compound.Id is not null) {
            expression += ".WithId(" + CSharpLiteral.Quote(compound.Id) + ")";
        }

        foreach (var className in compound.Classes) {
            expression += ".WithClass(" + CSharpLiteral.Quote(className) + ")";
        }

        foreach (var state in compound.States) {
            expression += ".WithState(" + CSharpLiteral.Quote(state) + ")";
        }

        return includePseudoElement
            ? AppendPseudoElement(expression, compound)
            : expression;
    }

    private static string AppendPseudoElement(
        string expression,
        CssCompoundSelector compound) {
        if (compound.PseudoElement is not null) {
            expression += ".WithPseudoElement(" +
                CSharpLiteral.Quote(compound.PseudoElement) +
                ")";
        }

        foreach (var state in compound.PseudoElementStates) {
            expression += ".WithPseudoElementState(" +
                CSharpLiteral.Quote(state) +
                ")";
        }

        return expression;
    }
}
