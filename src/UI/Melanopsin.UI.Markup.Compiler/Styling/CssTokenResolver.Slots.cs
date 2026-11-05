// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssTokenResolver {
    public static bool ResolveSlot(
        CssTokenState state,
        IReadOnlyDictionary<string, CssTokenState> states,
        List<CssDiagnostic> diagnostics,
        List<CssTokenState> order) {
        if (state.Expression is not null) {
            return true;
        }

        if (state.IsResolving) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                $"Style token '${state.Slot.Name}' contains a reference cycle.",
                state.Slot.Offset,
                state.Slot.Length));
            return false;
        }

        state.IsResolving = true;
        string? expression;
        if (CssTokenReference.TryParse(state.Slot.Value, out var reference)) {
            var name = reference.Name;
            if (!states.TryGetValue(name, out var referenced)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.UnknownStyleSlot,
                    $"Unknown style token '${name}' referenced by '${state.Slot.Name}'.",
                    state.Slot.Offset,
                    state.Slot.Length));
                expression = null;
            } else if (referenced.Kind != state.Kind) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.StyleSlotTypeMismatch,
                    $"Style token '${state.Slot.Name}' is '{CssValueSyntax.TokenTypeName(state.Kind)}', but '${name}' is '{CssValueSyntax.TokenTypeName(referenced.Kind)}'.",
                    state.Slot.Offset,
                    state.Slot.Length));
                expression = null;
            } else if (!ResolveSlot(referenced, states, diagnostics, order)) {
                expression = null;
            } else {
                expression = "Tokens." + referenced.CSharpName;
            }
        } else if (state.Slot.Value.IndexOf('$') >= 0) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                "A style token reference must be the entire token value.",
                state.Slot.Offset,
                state.Slot.Length));
            expression = null;
        } else {
            expression = CssLiteralParser.ParseLiteral(
                state.Slot.Value,
                state.Kind,
                state.Slot.Offset,
                diagnostics);
        }

        state.IsResolving = false;
        if (expression is null) {
            return false;
        }

        state.Expression = expression;
        order.Add(state);
        return true;
    }
}
