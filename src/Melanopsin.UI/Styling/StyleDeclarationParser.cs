using Melanopsin.UI;
using Melanopsin.UI.Motion;

namespace Melanopsin.UI.Styling;

internal sealed class StyleDeclarationParser {
    private readonly UiMetrics _metrics;

    public StyleDeclarationParser(UiMetrics metrics) {
        _metrics = metrics;
    }

    public void ApplyInline(ResolvedStyle style, Element element) {
        ApplyInlineStyle(style, element.InlineStyle);
        if (element.Attributes.TryGetValue("style", out var inline) &&
            inline is string inlineText) {
            Apply(style, ParseInline(inlineText));
        }

        if (element.Attributes.TryGetValue("font", out var font) &&
            font is FontFace fontFace) {
            style.Font = fontFace;
        }
    }

    private static Dictionary<string, string> ParseInline(string source) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in source.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            var separator = declaration.IndexOf(':');
            if (separator <= 0) {
                continue;
            }

            result[declaration[..separator].Trim()] =
                declaration[(separator + 1)..].Trim();
        }

        return result;
    }

    public void Apply(ResolvedStyle style, IReadOnlyDictionary<string, string> declarations) {
        foreach (var declaration in declarations) {
            switch (declaration.Key) {
                case "background":
                    style.Background = ParseColor(declaration.Value, style.Background);
                    break;
                case "color":
                    style.Foreground = ParseColor(declaration.Value, style.Foreground);
                    break;
                case "border-color":
                    style.BorderColor = ParseColor(declaration.Value, style.BorderColor);
                    break;
                case "border-width":
                    style.BorderWidth = ParseDimension(
                        style,
                        declaration.Value,
                        style.BorderWidth);
                    break;
                case "border-radius":
                    style.BorderRadius = ParseDimension(
                        style,
                        declaration.Value,
                        style.BorderRadius);
                    break;
                case "padding":
                    style.Padding = ParseDimension(style, declaration.Value, style.Padding);
                    break;
                case "margin":
                    style.Margin = ParseDimension(style, declaration.Value, style.Margin);
                    break;
                case "width":
                    style.Width = ParseLength(style, declaration.Value, style.Width);
                    break;
                case "height":
                    style.Height = ParseLength(style, declaration.Value, style.Height);
                    break;
                case "min-width":
                    style.MinWidth = ParseLength(style, declaration.Value, style.MinWidth);
                    break;
                case "max-width":
                    style.MaxWidth = ParseLength(style, declaration.Value, style.MaxWidth);
                    break;
                case "min-height":
                    style.MinHeight = ParseLength(style, declaration.Value, style.MinHeight);
                    break;
                case "max-height":
                    style.MaxHeight = ParseLength(style, declaration.Value, style.MaxHeight);
                    break;
                case "flex-grow":
                    style.FlexGrow = Math.Max(0, ParseFloat(declaration.Value, style.FlexGrow));
                    break;
                case "flex-shrink":
                    style.FlexShrink = Math.Max(0, ParseFloat(declaration.Value, style.FlexShrink));
                    break;
                case "weight":
                    style.Weight = Math.Max(0, ParseFloat(declaration.Value, style.Weight));
                    break;
                case "order":
                    style.Order = ParseInteger(declaration.Value, style.Order);
                    break;
                case "grid-column":
                    style.GridColumn = ParseInteger(declaration.Value, style.GridColumn);
                    break;
                case "grid-row":
                    style.GridRow = ParseInteger(declaration.Value, style.GridRow);
                    break;
                case "grid-column-span":
                    style.GridColumnSpan = Math.Max(
                        1,
                        ParseInteger(declaration.Value, style.GridColumnSpan));
                    break;
                case "grid-row-span":
                    style.GridRowSpan = Math.Max(
                        1,
                        ParseInteger(declaration.Value, style.GridRowSpan));
                    break;
                case "align-self":
                    style.AlignSelf = ControlValueParser.ReadLayoutAlignment(
                        declaration.Value,
                        style.AlignSelf);
                    break;
                case "anchor":
                    style.Anchor = ControlValueParser.ReadAnchor(declaration.Value);
                    break;
                case "font-size":
                    style.FontSize = ParseFontSize(style, declaration.Value, style.FontSize);
                    break;
                case "text-align":
                    style.TextHorizontalAlignment = ParseHorizontalAlignment(
                        declaration.Value,
                        style.TextHorizontalAlignment);
                    break;
                case "vertical-align":
                    style.TextVerticalAlignment = ParseVerticalAlignment(
                        declaration.Value,
                        style.TextVerticalAlignment);
                    break;
                case "opacity":
                    style.Opacity = Math.Clamp(ParseFloat(declaration.Value, style.Opacity), 0, 1);
                    break;
                case "transition-duration":
                    style.TransitionDuration = UiDuration.TryParse(
                        declaration.Value,
                        out var duration)
                        ? Math.Max(0, duration.Milliseconds / 1000f)
                        : style.TransitionDuration;
                    break;
                case "transition-easing":
                    style.TransitionEasing = EasingParser.TryParse(
                        declaration.Value,
                        out var easing)
                        ? easing
                        : style.TransitionEasing;
                    break;
                case "cursor":
                    style.Cursor = ParseCursor(declaration.Value, style.Cursor);
                    break;
            }
        }
    }

    private static float ParseFloat(string value, float fallback) {
        return float.TryParse(
            value.TrimEnd("px".ToCharArray()),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private float ParseFontSize(
        ResolvedStyle style,
        string value,
        float fallback) {
        if (!Length.TryParse(value, out var length)) {
            return fallback;
        }

        style.UsesViewportUnits |= length.UsesViewportUnits;
        return Math.Max(
            1,
            length.Resolve(
                _metrics,
                available: _metrics.ViewportWidth,
                fallback,
                emSize: fallback));
    }

    private float ParseDimension(
        ResolvedStyle style,
        string value,
        float fallback) {
        if (!Length.TryParse(value, out var length)) {
            return ParseFloat(value, fallback);
        }

        style.UsesViewportUnits |= length.UsesViewportUnits;
        return Math.Max(
            0,
            length.Resolve(
                _metrics,
                available: _metrics.ViewportWidth,
                fallback));
    }

    private static Length ParseLength(
        ResolvedStyle style,
        string value,
        Length? fallback) {
        if (!Length.TryParse(value, out var length)) {
            return fallback ?? Length.Auto;
        }

        style.UsesViewportUnits |= length.UsesViewportUnits;
        return length;
    }

    private static int ParseInteger(string value, int fallback) {
        return int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static void ApplyInlineStyle(ResolvedStyle style, InlineStyle source) {
        style.Width = ApplyLength(style, source.Width, style.Width);
        style.Height = ApplyLength(style, source.Height, style.Height);
        style.MinWidth = ApplyLength(style, source.MinWidth, style.MinWidth);
        style.MaxWidth = ApplyLength(style, source.MaxWidth, style.MaxWidth);
        style.MinHeight = ApplyLength(style, source.MinHeight, style.MinHeight);
        style.MaxHeight = ApplyLength(style, source.MaxHeight, style.MaxHeight);
        style.FlexGrow = source.FlexGrow ?? style.FlexGrow;
        style.FlexShrink = source.FlexShrink ?? style.FlexShrink;
        style.Weight = source.Weight ?? style.Weight;
        style.Order = source.Order ?? style.Order;
        style.GridRow = source.GridRow ?? style.GridRow;
        style.GridColumn = source.GridColumn ?? style.GridColumn;
        style.GridRowSpan = source.GridRowSpan ?? style.GridRowSpan;
        style.GridColumnSpan = source.GridColumnSpan ?? style.GridColumnSpan;
        style.AlignSelf = source.AlignSelf ?? style.AlignSelf;
    }

    private static Length ApplyLength(
        ResolvedStyle style,
        Length? value,
        Length target) {
        if (value is null) {
            return target;
        }

        style.UsesViewportUnits |= value.Value.UsesViewportUnits;
        return value.Value;
    }

    private static Length? ApplyLength(
        ResolvedStyle style,
        Length? value,
        Length? target) {
        if (value is null) {
            return target;
        }

        style.UsesViewportUnits |= value.Value.UsesViewportUnits;
        return value.Value;
    }

    private static TextHorizontalAlignment ParseHorizontalAlignment(
        string value,
        TextHorizontalAlignment fallback) {
        return value.ToLowerInvariant() switch {
            "start" or "left" => TextHorizontalAlignment.Start,
            "center" => TextHorizontalAlignment.Center,
            "end" or "right" => TextHorizontalAlignment.End,
            _ => fallback,
        };
    }

    private static TextVerticalAlignment ParseVerticalAlignment(
        string value,
        TextVerticalAlignment fallback) {
        return value.ToLowerInvariant() switch {
            "top" => TextVerticalAlignment.Top,
            "center" => TextVerticalAlignment.Center,
            "bottom" => TextVerticalAlignment.Bottom,
            _ => fallback,
        };
    }

    private static CursorKind? ParseCursor(string value, CursorKind? fallback) {
        return value.Trim().ToLowerInvariant() switch {
            "auto" => CursorKind.Auto,
            "default" => CursorKind.Default,
            "none" => CursorKind.None,
            "context-menu" => CursorKind.ContextMenu,
            "help" => CursorKind.Help,
            "pointer" => CursorKind.Pointer,
            "progress" => CursorKind.Progress,
            "wait" => CursorKind.Wait,
            "cell" => CursorKind.Cell,
            "crosshair" => CursorKind.Crosshair,
            "text" => CursorKind.Text,
            "vertical-text" => CursorKind.VerticalText,
            "alias" => CursorKind.Alias,
            "copy" => CursorKind.Copy,
            "move" => CursorKind.Move,
            "no-drop" => CursorKind.NoDrop,
            "not-allowed" => CursorKind.NotAllowed,
            "grab" => CursorKind.Grab,
            "grabbing" => CursorKind.Grabbing,
            "all-scroll" => CursorKind.AllScroll,
            "col-resize" => CursorKind.ColResize,
            "row-resize" => CursorKind.RowResize,
            "n-resize" => CursorKind.NResize,
            "e-resize" => CursorKind.EResize,
            "s-resize" => CursorKind.SResize,
            "w-resize" => CursorKind.WResize,
            "ne-resize" => CursorKind.NeResize,
            "nw-resize" => CursorKind.NwResize,
            "se-resize" => CursorKind.SeResize,
            "sw-resize" => CursorKind.SwResize,
            "ew-resize" => CursorKind.EwResize,
            "ns-resize" => CursorKind.NsResize,
            "nesw-resize" => CursorKind.NeswResize,
            "nwse-resize" => CursorKind.NwseResize,
            "zoom-in" => CursorKind.ZoomIn,
            "zoom-out" => CursorKind.ZoomOut,
            "dnd-ask" => CursorKind.DndAsk,
            "all-resize" => CursorKind.AllResize,
            _ => fallback,
        };
    }

    private static Color ParseColor(string value, Color fallback) {
        if (ColorParser.TryParse(value, out var color)) {
            return color;
        }

        return value.ToLowerInvariant() switch {
            "primary" => new Color(103, 80, 164),
            "surface" => new Color(255, 251, 254),
            "surface-variant" => new Color(231, 224, 236),
            "error" => new Color(179, 38, 30),
            "white" => new Color(255, 255, 255),
            "black" => new Color(0, 0, 0),
            "transparent" => new Color(0, 0, 0, 0),
            _ => fallback,
        };
    }
}
