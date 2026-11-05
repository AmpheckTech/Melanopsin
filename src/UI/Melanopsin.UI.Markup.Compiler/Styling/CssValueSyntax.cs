// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal enum StyleValueKind {
    Color,
    Length,
    Number,
    Integer,
    Duration,
    Easing,
    LayoutAlignment,
    Anchor,
    TextAlignment,
    VerticalAlignment,
    Cursor,
    Blur,
}

internal sealed class CssPropertyDefinition {
    public CssPropertyDefinition(string member, StyleValueKind kind) {
        Member = member;
        Kind = kind;
    }

    public string Member { get; }

    public StyleValueKind Kind { get; }
}

internal static class CssValueSyntax {
    public static readonly HashSet<string> CursorNames = new(StringComparer.OrdinalIgnoreCase) {
        "auto", "default", "none", "context-menu", "help", "pointer", "progress",
        "wait", "cell", "crosshair", "text", "vertical-text", "alias", "copy",
        "move", "no-drop", "not-allowed", "grab", "grabbing", "all-scroll",
        "col-resize", "row-resize", "n-resize", "e-resize", "s-resize", "w-resize",
        "ne-resize", "nw-resize", "se-resize", "sw-resize", "ew-resize",
        "ns-resize", "nesw-resize", "nwse-resize", "zoom-in", "zoom-out",
        "dnd-ask", "all-resize",
    };

    public static bool TryGetProperty(string name, out CssPropertyDefinition definition) {
        definition = name.ToLowerInvariant() switch {
            "background" => new CssPropertyDefinition("Background", StyleValueKind.Color),
            "color" => new CssPropertyDefinition("Foreground", StyleValueKind.Color),
            "border-color" => new CssPropertyDefinition("BorderColor", StyleValueKind.Color),
            "border-width" => new CssPropertyDefinition("BorderWidth", StyleValueKind.Length),
            "border-radius" => new CssPropertyDefinition("BorderRadius", StyleValueKind.Length),
            "padding" => new CssPropertyDefinition("Padding", StyleValueKind.Length),
            "margin" => new CssPropertyDefinition("Margin", StyleValueKind.Length),
            "width" => new CssPropertyDefinition("Width", StyleValueKind.Length),
            "height" => new CssPropertyDefinition("Height", StyleValueKind.Length),
            "min-width" => new CssPropertyDefinition("MinWidth", StyleValueKind.Length),
            "max-width" => new CssPropertyDefinition("MaxWidth", StyleValueKind.Length),
            "min-height" => new CssPropertyDefinition("MinHeight", StyleValueKind.Length),
            "max-height" => new CssPropertyDefinition("MaxHeight", StyleValueKind.Length),
            "font-size" => new CssPropertyDefinition("FontSize", StyleValueKind.Length),
            "flex-grow" => new CssPropertyDefinition("FlexGrow", StyleValueKind.Number),
            "flex-shrink" => new CssPropertyDefinition("FlexShrink", StyleValueKind.Number),
            "weight" => new CssPropertyDefinition("Weight", StyleValueKind.Number),
            "opacity" => new CssPropertyDefinition("Opacity", StyleValueKind.Number),
            "filter" => new CssPropertyDefinition("Filter", StyleValueKind.Blur),
            "backdrop-filter" => new CssPropertyDefinition(
                "BackdropFilter",
                StyleValueKind.Blur),
            "order" => new CssPropertyDefinition("Order", StyleValueKind.Integer),
            "grid-row" => new CssPropertyDefinition("GridRow", StyleValueKind.Integer),
            "grid-column" => new CssPropertyDefinition("GridColumn", StyleValueKind.Integer),
            "grid-row-span" => new CssPropertyDefinition("GridRowSpan", StyleValueKind.Integer),
            "grid-column-span" => new CssPropertyDefinition("GridColumnSpan", StyleValueKind.Integer),
            "align-self" => new CssPropertyDefinition("AlignSelf", StyleValueKind.LayoutAlignment),
            "anchor" => new CssPropertyDefinition("Anchor", StyleValueKind.Anchor),
            "text-align" => new CssPropertyDefinition(
                "TextHorizontalAlignment",
                StyleValueKind.TextAlignment),
            "vertical-align" => new CssPropertyDefinition(
                "TextVerticalAlignment",
                StyleValueKind.VerticalAlignment),
            "transition-duration" => new CssPropertyDefinition(
                "TransitionDuration",
                StyleValueKind.Duration),
            "transition-easing" => new CssPropertyDefinition(
                "TransitionEasing",
                StyleValueKind.Easing),
            "cursor" => new CssPropertyDefinition("Cursor", StyleValueKind.Cursor),
            "scrollbar-thumb-color" => new CssPropertyDefinition(
                "ScrollbarThumbColor",
                StyleValueKind.Color),
            "scrollbar-track-color" => new CssPropertyDefinition(
                "ScrollbarTrackColor",
                StyleValueKind.Color),
            "scrollbar-thickness" => new CssPropertyDefinition(
                "ScrollbarThickness",
                StyleValueKind.Length),
            "scrollbar-min-length" => new CssPropertyDefinition(
                "ScrollbarMinLength",
                StyleValueKind.Length),
            "scrollbar-inset" => new CssPropertyDefinition(
                "ScrollbarInset",
                StyleValueKind.Length),
            _ => null!,
        };
        return definition is not null;
    }

    public static bool TryGetTokenKind(string name, out StyleValueKind kind) {
        switch (name.Trim().ToLowerInvariant()) {
            case "color":
                kind = StyleValueKind.Color;
                return true;
            case "length":
                kind = StyleValueKind.Length;
                return true;
            case "number":
                kind = StyleValueKind.Number;
                return true;
            case "integer":
                kind = StyleValueKind.Integer;
                return true;
            case "duration":
                kind = StyleValueKind.Duration;
                return true;
            case "easing":
                kind = StyleValueKind.Easing;
                return true;
            case "layout-alignment":
                kind = StyleValueKind.LayoutAlignment;
                return true;
            case "anchor":
                kind = StyleValueKind.Anchor;
                return true;
            case "text-alignment":
                kind = StyleValueKind.TextAlignment;
                return true;
            case "vertical-alignment":
                kind = StyleValueKind.VerticalAlignment;
                return true;
            case "cursor":
                kind = StyleValueKind.Cursor;
                return true;
            case "blur":
                kind = StyleValueKind.Blur;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string RuntimeTypeName(StyleValueKind kind) {
        return kind switch {
            StyleValueKind.Color => "global::Melanopsin.Graphics.Color",
            StyleValueKind.Length => "global::Melanopsin.UI.Length",
            StyleValueKind.Number => "float",
            StyleValueKind.Integer => "int",
            StyleValueKind.Duration => "global::Melanopsin.UI.Motion.UiDuration",
            StyleValueKind.Easing => "global::Melanopsin.UI.Animation.Easing",
            StyleValueKind.LayoutAlignment => "global::Melanopsin.UI.LayoutAlignment",
            StyleValueKind.Anchor => "global::Melanopsin.UI.Anchor",
            StyleValueKind.TextAlignment =>
                "global::Melanopsin.Graphics.Text.TextHorizontalAlignment",
            StyleValueKind.VerticalAlignment =>
                "global::Melanopsin.Graphics.Text.TextVerticalAlignment",
            StyleValueKind.Cursor => "global::Melanopsin.UI.CursorKind",
            StyleValueKind.Blur => "global::Melanopsin.UI.Styling.BlurFilter",
            _ => "object",
        };
    }

    public static string TokenTypeName(StyleValueKind kind) {
        return kind switch {
            StyleValueKind.Color => "color",
            StyleValueKind.Length => "length",
            StyleValueKind.Number => "number",
            StyleValueKind.Integer => "integer",
            StyleValueKind.Duration => "duration",
            StyleValueKind.Easing => "easing",
            StyleValueKind.LayoutAlignment => "layout-alignment",
            StyleValueKind.Anchor => "anchor",
            StyleValueKind.TextAlignment => "text-alignment",
            StyleValueKind.VerticalAlignment => "vertical-alignment",
            StyleValueKind.Cursor => "cursor",
            StyleValueKind.Blur => "blur",
            _ => "unknown",
        };
    }
}
