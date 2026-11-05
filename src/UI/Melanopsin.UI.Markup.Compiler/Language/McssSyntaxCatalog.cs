// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Styling;

namespace Melanopsin.UI.Markup.Compiler.Language;

internal static class McssSyntaxCatalog {
    private static readonly string[] PropertyCandidates = [
        "background", "color", "border-color", "border-width", "border-radius",
        "padding", "margin", "width", "height", "min-width", "max-width",
        "min-height", "max-height", "font-size", "flex-grow", "flex-shrink",
        "weight", "opacity", "filter", "backdrop-filter", "order", "grid-row",
        "grid-column", "grid-row-span", "grid-column-span", "align-self", "anchor",
        "text-align", "vertical-align", "transition-duration", "transition-easing",
        "cursor", "scrollbar-thumb-color", "scrollbar-track-color",
        "scrollbar-thickness", "scrollbar-min-length", "scrollbar-inset",
    ];

    private static readonly string[] TokenTypeNames = [
        "color", "length", "number", "integer", "duration", "easing",
        "layout-alignment", "anchor", "text-alignment", "vertical-alignment",
        "cursor", "blur",
    ];

    public static IReadOnlyList<string> Properties { get; } = PropertyCandidates;

    public static IReadOnlyList<string> TokenTypes { get; } = TokenTypeNames;

    public static IReadOnlyList<McssCompletionItem> PropertyCompletions() {
        return PropertyCandidates.Select(property => {
            CssValueSyntax.TryGetProperty(property, out var definition);
            var type = CssValueSyntax.TokenTypeName(definition.Kind);
            return new McssCompletionItem(
                property,
                type,
                property,
                McssCompletionKind.Property,
                $"Sets `{type}` on `{definition.Member}`.");
        }).ToArray();
    }

    public static IReadOnlyList<McssCompletionItem> TokenTypeCompletions() {
        return TokenTypeNames.Select(type => new McssCompletionItem(
            type,
            "MCSS token type",
            type,
            McssCompletionKind.Value,
            $"Declares a typed `{type}` token.")).ToArray();
    }

    public static IReadOnlyList<McssCompletionItem> TokenValueCompletions(string typeName) {
        return CssValueSyntax.TryGetTokenKind(typeName, out var kind)
            ? ValueCompletions(kind, $"MCSS `{typeName}` token")
            : Array.Empty<McssCompletionItem>();
    }

    public static IReadOnlyList<McssCompletionItem> ValueCompletions(string property) {
        if (!CssValueSyntax.TryGetProperty(property, out var definition)) {
            return Array.Empty<McssCompletionItem>();
        }

        return ValueCompletions(
            definition.Kind,
            CssValueSyntax.TokenTypeName(definition.Kind));
    }

    private static IReadOnlyList<McssCompletionItem> ValueCompletions(
        StyleValueKind kind,
        string detail) {
        return ValueCandidates(kind)
            .Select(value => new McssCompletionItem(
                value,
                detail,
                value,
                McssCompletionKind.Value))
            .ToArray();
    }

    public static bool IsKnownValue(StyleValueKind kind, string value) {
        return ValueCandidates(kind).Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ValueCandidates(StyleValueKind kind) {
        return kind switch {
            StyleValueKind.Color => ["transparent", "#00000000"],
            StyleValueKind.Length => ["0px", "1px", "1su", "1ru", "1fu", "1hx", "1iu"],
            StyleValueKind.Number => ["0", "1"],
            StyleValueKind.Integer => ["0", "1"],
            StyleValueKind.Duration => ["0ms", "120ms", "1s"],
            StyleValueKind.Easing => ["linear", "ease-in", "ease-out", "ease-in-out", "spring"],
            StyleValueKind.LayoutAlignment => ["auto", "start", "center", "end", "stretch"],
            StyleValueKind.Anchor => [
                "top-left", "top", "top-right", "left", "center", "right",
                "bottom-left", "bottom", "bottom-right",
            ],
            StyleValueKind.TextAlignment => ["start", "center", "end"],
            StyleValueKind.VerticalAlignment => ["top", "center", "bottom"],
            StyleValueKind.Cursor => CssValueSyntax.CursorNames.OrderBy(
                    static name => name,
                    StringComparer.Ordinal).ToArray(),
            StyleValueKind.Blur => ["none", "blur(1su)"],
            _ => Array.Empty<string>(),
        };
    }
}
