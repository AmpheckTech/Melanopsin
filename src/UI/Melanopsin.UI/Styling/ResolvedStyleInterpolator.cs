using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

internal static class ResolvedStyleInterpolator {
    public static ResolvedStyle Clone(ResolvedStyle source) {
        return new ResolvedStyle {
            Background = source.Background,
            Foreground = source.Foreground,
            BorderColor = source.BorderColor,
            BorderWidth = source.BorderWidth,
            BorderRadius = source.BorderRadius,
            Padding = source.Padding,
            Margin = source.Margin,
            Width = source.Width,
            Height = source.Height,
            MinWidth = source.MinWidth,
            MaxWidth = source.MaxWidth,
            MinHeight = source.MinHeight,
            MaxHeight = source.MaxHeight,
            FlexGrow = source.FlexGrow,
            FlexShrink = source.FlexShrink,
            Weight = source.Weight,
            Order = source.Order,
            GridRow = source.GridRow,
            GridColumn = source.GridColumn,
            GridRowSpan = source.GridRowSpan,
            GridColumnSpan = source.GridColumnSpan,
            AlignSelf = source.AlignSelf,
            Font = source.Font,
            FontSize = source.FontSize,
            TextHorizontalAlignment = source.TextHorizontalAlignment,
            TextVerticalAlignment = source.TextVerticalAlignment,
            Opacity = source.Opacity,
            FilterBlurRadius = source.FilterBlurRadius,
            BackdropBlurRadius = source.BackdropBlurRadius,
            TransitionDuration = source.TransitionDuration,
            TransitionEasing = source.TransitionEasing,
            Cursor = source.Cursor,
            ScrollbarThumbColor = source.ScrollbarThumbColor,
            ScrollbarTrackColor = source.ScrollbarTrackColor,
            ScrollbarThickness = source.ScrollbarThickness,
            ScrollbarMinLength = source.ScrollbarMinLength,
            ScrollbarInset = source.ScrollbarInset,
            Anchor = source.Anchor,
            UsesViewportUnits = source.UsesViewportUnits,
        };
    }

    public static ResolvedStyle Interpolate(
        ResolvedStyle from,
        ResolvedStyle to,
        float amount) {
        return new ResolvedStyle {
            Background = Lerp(from.Background, to.Background, amount),
            Foreground = Lerp(from.Foreground, to.Foreground, amount),
            BorderColor = Lerp(from.BorderColor, to.BorderColor, amount),
            BorderWidth = Lerp(from.BorderWidth, to.BorderWidth, amount),
            BorderRadius = Lerp(from.BorderRadius, to.BorderRadius, amount),
            Padding = to.Padding,
            Margin = to.Margin,
            Width = to.Width,
            Height = to.Height,
            MinWidth = to.MinWidth,
            MaxWidth = to.MaxWidth,
            MinHeight = to.MinHeight,
            MaxHeight = to.MaxHeight,
            FlexGrow = to.FlexGrow,
            FlexShrink = to.FlexShrink,
            Weight = to.Weight,
            Order = to.Order,
            GridRow = to.GridRow,
            GridColumn = to.GridColumn,
            GridRowSpan = to.GridRowSpan,
            GridColumnSpan = to.GridColumnSpan,
            AlignSelf = to.AlignSelf,
            Font = to.Font,
            FontSize = to.FontSize,
            TextHorizontalAlignment = amount < 0.5
                ? from.TextHorizontalAlignment
                : to.TextHorizontalAlignment,
            TextVerticalAlignment = amount < 0.5
                ? from.TextVerticalAlignment
                : to.TextVerticalAlignment,
            Opacity = Lerp(from.Opacity, to.Opacity, amount),
            FilterBlurRadius = Lerp(
                from.FilterBlurRadius,
                to.FilterBlurRadius,
                amount),
            BackdropBlurRadius = Lerp(
                from.BackdropBlurRadius,
                to.BackdropBlurRadius,
                amount),
            TransitionDuration = to.TransitionDuration,
            TransitionEasing = to.TransitionEasing,
            Cursor = to.Cursor,
            ScrollbarThumbColor = to.ScrollbarThumbColor,
            ScrollbarTrackColor = to.ScrollbarTrackColor,
            ScrollbarThickness = to.ScrollbarThickness,
            ScrollbarMinLength = to.ScrollbarMinLength,
            ScrollbarInset = to.ScrollbarInset,
            Anchor = to.Anchor,
            UsesViewportUnits = to.UsesViewportUnits,
        };
    }

    private static Color Lerp(Color from, Color to, float amount) {
        return new Color(
            Lerp(from.Red, to.Red, amount),
            Lerp(from.Green, to.Green, amount),
            Lerp(from.Blue, to.Blue, amount),
            Lerp(from.Alpha, to.Alpha, amount));
    }

    private static byte Lerp(byte from, byte to, float amount) {
        return (byte)Math.Clamp(
            MathF.Round(from + ((to - from) * amount)),
            0,
            255);
    }

    private static float Lerp(float from, float to, float amount) {
        return from + ((to - from) * amount);
    }

}
