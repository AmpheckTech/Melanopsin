using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssTokenResolver {
    public static string? ResolveValue(
        string value,
        StyleValueKind expectedKind,
        int offset,
        IReadOnlyDictionary<string, CssTokenState> states,
        List<CssDiagnostic> diagnostics) {
        if (CssTokenReference.TryParse(value, out var reference)) {
            var name = reference.Name;
            if (!states.TryGetValue(name, out var state)) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.UnknownStyleSlot,
                    $"Unknown style token '${name}'.",
                    offset,
                    Math.Max(1, value.Length)));
                return null;
            }

            if (state.Kind != expectedKind) {
                diagnostics.Add(new CssDiagnostic(
                    MarkupDiagnosticCodes.StyleSlotTypeMismatch,
                    $"Style token '${name}' has type '{CssValueSyntax.TokenTypeName(state.Kind)}' and cannot be used where '{CssValueSyntax.TokenTypeName(expectedKind)}' is required.",
                    offset,
                    Math.Max(1, value.Length)));
                return null;
            }

            return "Tokens." + state.CSharpName;
        }

        if (value.IndexOf('$') >= 0) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                "A style token reference must be the entire property value.",
                offset,
                Math.Max(1, value.Length)));
            return null;
        }

        return CssLiteralParser.ParseLiteral(value, expectedKind, offset, diagnostics);
    }
}
