using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

internal static class ControlValueParser {
    public static float? ReadNumber(object? value) {
        if (value is null) {
            return null;
        }

        if (value is IConvertible convertible) {
            try {
                return convertible.ToSingle(System.Globalization.CultureInfo.InvariantCulture);
            } catch (FormatException) {
                return null;
            } catch (InvalidCastException) {
                return null;
            }
        }

        return float.TryParse(
            value.ToString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    public static Color? ReadColor(object? value) {
        if (value is not string text) {
            return null;
        }

        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase)) {
            return new Color(0, 0, 0, 0);
        }

        if (text.Length != 7 || text[0] != '#' ||
            !byte.TryParse(text.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red) ||
            !byte.TryParse(text.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
            !byte.TryParse(text.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)) {
            return null;
        }

        return new Color(red, green, blue);
    }

    public static int? ReadInteger(object? value) {
        if (value is null) {
            return null;
        }

        return int.TryParse(
            value.ToString(),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    public static bool? ReadBoolean(object? value) {
        if (value is null) {
            return null;
        }

        return bool.TryParse(value.ToString(), out var parsed)
            ? parsed
            : null;
    }

    public static Anchor ReadAnchor(object? value) {
        return value?.ToString()?.ToLowerInvariant() switch {
            "top" => Anchor.Top,
            "top-right" => Anchor.TopRight,
            "left" => Anchor.Left,
            "center" => Anchor.Center,
            "right" => Anchor.Right,
            "bottom-left" => Anchor.BottomLeft,
            "bottom" => Anchor.Bottom,
            "bottom-right" => Anchor.BottomRight,
            _ => Anchor.TopLeft,
        };
    }

    public static FlexDirection ReadFlexDirection(
        object? value,
        FlexDirection fallback) {
        return value?.ToString()?.ToLowerInvariant().Replace("-", string.Empty) switch {
            "row" => FlexDirection.Row,
            "rowreverse" => FlexDirection.RowReverse,
            "column" => FlexDirection.Column,
            "columnreverse" => FlexDirection.ColumnReverse,
            _ => fallback,
        };
    }

    public static FlexWrap ReadFlexWrap(object? value, FlexWrap fallback) {
        return value?.ToString()?.ToLowerInvariant().Replace("-", string.Empty) switch {
            "nowrap" => FlexWrap.NoWrap,
            "wrap" => FlexWrap.Wrap,
            "wrapreverse" => FlexWrap.WrapReverse,
            _ => fallback,
        };
    }

    public static JustifyContent ReadJustifyContent(
        object? value,
        JustifyContent fallback) {
        return value?.ToString()?.ToLowerInvariant().Replace("-", string.Empty) switch {
            "start" => JustifyContent.Start,
            "end" => JustifyContent.End,
            "center" => JustifyContent.Center,
            "spacebetween" => JustifyContent.SpaceBetween,
            "spacearound" => JustifyContent.SpaceAround,
            "spaceevenly" => JustifyContent.SpaceEvenly,
            _ => fallback,
        };
    }

    public static LayoutAlignment ReadLayoutAlignment(
        object? value,
        LayoutAlignment fallback) {
        return value?.ToString()?.ToLowerInvariant() switch {
            "auto" => LayoutAlignment.Auto,
            "start" or "flex-start" => LayoutAlignment.Start,
            "center" => LayoutAlignment.Center,
            "end" or "flex-end" => LayoutAlignment.End,
            "stretch" => LayoutAlignment.Stretch,
            _ => fallback,
        };
    }
}
