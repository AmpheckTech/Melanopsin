using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssParser {
    private static void AddRule(
        CssNestedRule rule,
        IReadOnlyList<CssComplexSelector>? parentSelectors,
        List<CssRule> rules,
        List<CssDiagnostic> diagnostics) {
        if (rule.Selector.Length == 0) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                "Style rule has an empty selector.",
                rule.Offset,
                1));
            return;
        }

        var selectors = new List<CssComplexSelector>();
        foreach (var part in SplitSelectors(rule.Selector, rule.Offset)) {
            if (parentSelectors is null) {
                if (!CssSelectorParser.TryParse(
                        part.Value,
                        part.Offset,
                        out var selector,
                        out var diagnostic)) {
                    if (diagnostic is not null) {
                        diagnostics.Add(diagnostic);
                    }
                } else if (selector is not null) {
                    selectors.Add(selector);
                }

                continue;
            }

            var nestedFailed = false;
            foreach (var parent in parentSelectors) {
                if (!CssSelectorParser.TryParseNested(
                        part.Value,
                        part.Offset,
                        parent,
                        out var selector,
                        out var diagnostic)) {
                    if (!nestedFailed && diagnostic is not null) {
                        diagnostics.Add(diagnostic);
                    }

                    nestedFailed = true;
                    break;
                }

                if (selector is not null) {
                    selectors.Add(selector);
                }
            }
        }

        var declarations = new List<CssDeclaration>();
        foreach (var item in rule.Items) {
            if (item.Declaration is { } declaration) {
                declarations.Add(declaration);
                continue;
            }

            FlushDeclarations(selectors, declarations, rules, rule.Offset);
            if (item.Rule is { } nested) {
                AddRule(nested, selectors, rules, diagnostics);
            }
        }

        FlushDeclarations(selectors, declarations, rules, rule.Offset);
    }

    private static void FlushDeclarations(
        IReadOnlyList<CssComplexSelector> selectors,
        List<CssDeclaration> declarations,
        List<CssRule> rules,
        int offset) {
        if (declarations.Count == 0) {
            return;
        }

        foreach (var selector in selectors) {
            rules.Add(new CssRule(selector, offset, [.. declarations]));
        }

        declarations.Clear();
    }

}
