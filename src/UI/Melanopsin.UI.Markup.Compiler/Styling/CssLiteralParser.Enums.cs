namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    private static bool TryParseLayoutAlignment(string value, out string? expression) {
        expression = value.ToLowerInvariant() switch {
            "auto" => "global::Melanopsin.UI.LayoutAlignment.Auto",
            "start" or "flex-start" => "global::Melanopsin.UI.LayoutAlignment.Start",
            "center" => "global::Melanopsin.UI.LayoutAlignment.Center",
            "end" or "flex-end" => "global::Melanopsin.UI.LayoutAlignment.End",
            "stretch" => "global::Melanopsin.UI.LayoutAlignment.Stretch",
            _ => null,
        };
        return expression is not null;
    }

    private static bool TryParseAnchor(string value, out string? expression) {
        expression = value.ToLowerInvariant() switch {
            "top-left" => "global::Melanopsin.UI.Anchor.TopLeft",
            "top" => "global::Melanopsin.UI.Anchor.Top",
            "top-right" => "global::Melanopsin.UI.Anchor.TopRight",
            "left" => "global::Melanopsin.UI.Anchor.Left",
            "center" => "global::Melanopsin.UI.Anchor.Center",
            "right" => "global::Melanopsin.UI.Anchor.Right",
            "bottom-left" => "global::Melanopsin.UI.Anchor.BottomLeft",
            "bottom" => "global::Melanopsin.UI.Anchor.Bottom",
            "bottom-right" => "global::Melanopsin.UI.Anchor.BottomRight",
            _ => null,
        };
        return expression is not null;
    }

    private static bool TryParseTextAlignment(string value, out string? expression) {
        expression = value.ToLowerInvariant() switch {
            "start" or "left" => "global::Melanopsin.Graphics.Text.TextHorizontalAlignment.Start",
            "center" => "global::Melanopsin.Graphics.Text.TextHorizontalAlignment.Center",
            "end" or "right" => "global::Melanopsin.Graphics.Text.TextHorizontalAlignment.End",
            _ => null,
        };
        return expression is not null;
    }

    private static bool TryParseVerticalAlignment(string value, out string? expression) {
        expression = value.ToLowerInvariant() switch {
            "top" => "global::Melanopsin.Graphics.Text.TextVerticalAlignment.Top",
            "center" => "global::Melanopsin.Graphics.Text.TextVerticalAlignment.Center",
            "bottom" => "global::Melanopsin.Graphics.Text.TextVerticalAlignment.Bottom",
            _ => null,
        };
        return expression is not null;
    }

    private static bool TryParseCursor(string value, out string? expression) {
        if (!CssValueSyntax.CursorNames.Contains(value)) {
            expression = null;
            return false;
        }

        expression = "global::Melanopsin.UI.CursorKind." + CssNaming.PascalName(value);
        return true;
    }
}
