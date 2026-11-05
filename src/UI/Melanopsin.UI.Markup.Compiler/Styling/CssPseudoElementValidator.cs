// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static class CssPseudoElementValidator {
    private static readonly HashSet<string> SupportedTargets =
        new(StringComparer.Ordinal) { "thumb", "track" };

    private static readonly HashSet<string> SupportedProperties =
        new(StringComparer.OrdinalIgnoreCase) {
            "background",
            "border-color",
            "border-width",
            "border-radius",
            "opacity",
            "transition-duration",
            "transition-easing",
        };

    public static void Validate(CssRule rule, List<CssDiagnostic> diagnostics) {
        var target = rule.Selector.Rightmost.PseudoElement;
        if (target is null) {
            return;
        }

        var ownerType = rule.Selector.Rightmost.TypeName;
        if (ownerType is not null &&
            !string.Equals(ownerType, "ScrollViewport", StringComparison.Ordinal)) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                $"'{ownerType}' does not expose the '{target}' pseudo-element.",
                rule.Offset,
                1));
            return;
        }

        if (!SupportedTargets.Contains(target)) {
            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                $"Unknown pseudo-element '::{target}'. Supported targets are '::track' and '::thumb'.",
                rule.Offset,
                1));
            return;
        }

        foreach (var declaration in rule.Declarations) {
            if (declaration.Name.StartsWith("$", StringComparison.Ordinal) ||
                SupportedProperties.Contains(declaration.Name)) {
                continue;
            }

            diagnostics.Add(new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                $"Style property '{declaration.Name}' cannot be used on ScrollViewport::{target}. Set scrollbar geometry on ScrollViewport instead.",
                declaration.Offset,
                Math.Max(1, declaration.Length)));
        }
    }
}
