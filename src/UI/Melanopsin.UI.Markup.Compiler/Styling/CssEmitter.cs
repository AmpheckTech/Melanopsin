using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssEmission {
    public CssEmission(
        string className,
        string hintName,
        string? source,
        IReadOnlyList<CssDiagnostic> diagnostics) {
        ClassName = className;
        HintName = hintName;
        Source = source;
        Diagnostics = diagnostics;
    }

    public string ClassName { get; }

    public string HintName { get; }

    public string? Source { get; }

    public IReadOnlyList<CssDiagnostic> Diagnostics { get; }
}

internal static class CssEmitter {
    public static CssEmission Emit(
        CssStylesheet sheet,
        string rootNamespace,
        IReadOnlyList<CssStylesheet> imports) {
        var className = CssNaming.ClassName(sheet.Path);
        var diagnostics = new List<CssDiagnostic>(sheet.Diagnostics);
        var states = EmitTokens(sheet, diagnostics);
        var slotsInDependencyOrder = OrderTokens(states, diagnostics);
        var assignments = EmitRules(sheet, states, diagnostics);
        if (diagnostics.Count != 0) {
            return new CssEmission(
                className,
                CssNaming.HintName(sheet.Path, className),
                source: null,
                diagnostics);
        }

        var source = CssSourceWriter.Emit(
            rootNamespace,
            imports,
            className,
            slotsInDependencyOrder,
            assignments);
        return new CssEmission(
            className,
            CssNaming.HintName(sheet.Path, className),
            source,
            diagnostics);
    }

    private static Dictionary<string, CssTokenState> EmitTokens(
        CssStylesheet sheet,
        List<CssDiagnostic> diagnostics) {
        var states = new Dictionary<string, CssTokenState>(StringComparer.Ordinal);
        var csharpNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in sheet.Slots) {
            if (!CssValueSyntax.TryGetTokenKind(slot.TypeName, out var kind)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Unknown token type '{slot.TypeName}'. Expected color, length, number, integer, duration, easing, layout-alignment, anchor, text-alignment, vertical-alignment, cursor, or blur.",
                    slot.Offset,
                    Math.Max(1, slot.TypeName.Length)));
                continue;
            }

            var name = CssNaming.ToPascalIdentifier(slot.Name);
            if (csharpNames.TryGetValue(name, out var existing)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.InvalidStylesheet,
                    $"Style tokens '${existing}' and '${slot.Name}' both generate the C# name '{name}'.",
                    slot.Offset,
                    slot.Length));
                continue;
            }

            csharpNames.Add(name, slot.Name);
            states.Add(slot.Name, new CssTokenState(slot, kind, name));
        }

        return states;
    }

    private static List<CssTokenState> OrderTokens(
        IReadOnlyDictionary<string, CssTokenState> states,
        List<CssDiagnostic> diagnostics) {
        var slotsInDependencyOrder = new List<CssTokenState>();
        foreach (var state in states.Values) {
            CssTokenResolver.ResolveSlot(
                state,
                states,
                diagnostics,
                slotsInDependencyOrder);
        }

        return slotsInDependencyOrder;
    }

    private static List<CssRuleAssignment> EmitRules(
        CssStylesheet sheet,
        IReadOnlyDictionary<string, CssTokenState> states,
        List<CssDiagnostic> diagnostics) {
        var assignments = new List<CssRuleAssignment>();
        foreach (var rule in sheet.Rules) {
            if (CssRuleEmitter.Emit(rule, states, diagnostics) is { } assignment) {
                assignments.Add(assignment);
            }
        }

        return assignments;
    }
}
