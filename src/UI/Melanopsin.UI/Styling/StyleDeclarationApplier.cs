using Melanopsin.UI.Motion;

namespace Melanopsin.UI.Styling;

internal sealed class StyleDeclarationApplier {
    private readonly UiMetrics _metrics;

    public StyleDeclarationApplier(UiMetrics metrics) {
        _metrics = metrics;
    }

    public void Apply(ResolvedStyle style, StyleProperties properties) {
        style.Background = properties.Background ?? style.Background;
        style.Foreground = properties.Foreground ?? style.Foreground;
        style.BorderColor = properties.BorderColor ?? style.BorderColor;
        style.BorderWidth = ResolveDimension(
            style,
            properties.BorderWidth,
            style.BorderWidth);
        style.BorderRadius = ResolveDimension(
            style,
            properties.BorderRadius,
            style.BorderRadius);
        style.Padding = ResolveDimension(style, properties.Padding, style.Padding);
        style.Margin = ResolveDimension(style, properties.Margin, style.Margin);
        style.Width = ApplyLength(style, properties.Width, style.Width);
        style.Height = ApplyLength(style, properties.Height, style.Height);
        style.MinWidth = ApplyLength(style, properties.MinWidth, style.MinWidth);
        style.MaxWidth = ApplyLength(style, properties.MaxWidth, style.MaxWidth);
        style.MinHeight = ApplyLength(style, properties.MinHeight, style.MinHeight);
        style.MaxHeight = ApplyLength(style, properties.MaxHeight, style.MaxHeight);
        style.FlexGrow = Math.Max(0, properties.FlexGrow ?? style.FlexGrow);
        style.FlexShrink = Math.Max(0, properties.FlexShrink ?? style.FlexShrink);
        style.Weight = Math.Max(0, properties.Weight ?? style.Weight);
        style.Order = properties.Order ?? style.Order;
        style.GridColumn = properties.GridColumn ?? style.GridColumn;
        style.GridRow = properties.GridRow ?? style.GridRow;
        style.GridColumnSpan = Math.Max(
            1,
            properties.GridColumnSpan ?? style.GridColumnSpan);
        style.GridRowSpan = Math.Max(
            1,
            properties.GridRowSpan ?? style.GridRowSpan);
        style.AlignSelf = properties.AlignSelf ?? style.AlignSelf;
        style.Anchor = properties.Anchor ?? style.Anchor;
        style.FontSize = ResolveFontSize(style, properties.FontSize, style.FontSize);
        style.TextHorizontalAlignment =
            properties.TextHorizontalAlignment ?? style.TextHorizontalAlignment;
        style.TextVerticalAlignment =
            properties.TextVerticalAlignment ?? style.TextVerticalAlignment;
        style.Opacity = Math.Clamp(properties.Opacity ?? style.Opacity, 0, 1);
        style.FilterBlurRadius = ResolveBlurRadius(
            style,
            properties.Filter,
            style.FilterBlurRadius);
        style.BackdropBlurRadius = ResolveBlurRadius(
            style,
            properties.BackdropFilter,
            style.BackdropBlurRadius);
        if (properties.TransitionDuration is { } duration) {
            style.TransitionDuration = Math.Max(0, duration.Milliseconds / 1000f);
        }

        style.TransitionEasing =
            properties.TransitionEasing ?? style.TransitionEasing;
        style.Cursor = properties.Cursor ?? style.Cursor;
        style.ScrollbarThumbColor =
            properties.ScrollbarThumbColor ?? style.ScrollbarThumbColor;
        style.ScrollbarTrackColor =
            properties.ScrollbarTrackColor ?? style.ScrollbarTrackColor;
        style.ScrollbarThickness = ResolveDimension(
            style,
            properties.ScrollbarThickness,
            style.ScrollbarThickness);
        style.ScrollbarMinLength = ResolveDimension(
            style,
            properties.ScrollbarMinLength,
            style.ScrollbarMinLength);
        style.ScrollbarInset = ResolveDimension(
            style,
            properties.ScrollbarInset,
            style.ScrollbarInset);
    }

    public void ApplyInline(ResolvedStyle style, Element element) {
        if (element.HasInlineStyle) {
            ApplyInlineStyle(style, element.InlineStyle);
        }

        if (element.Attributes.TryGetValue("style", out var inline) &&
            inline is StyleProperties properties) {
            Apply(style, properties);
        }

        if (element.Attributes.TryGetValue("font", out var font) &&
            font is FontFace fontFace) {
            style.Font = fontFace;
        }
    }

    private float ResolveDimension(
        ResolvedStyle style,
        Length? value,
        float fallback) {
        if (value is null) {
            return fallback;
        }

        style.UsesViewportUnits |= value.Value.UsesViewportUnits;
        return Math.Max(
            0,
            value.Value.Resolve(
                _metrics,
                _metrics.ViewportWidth,
                fallback));
    }

    private float ResolveFontSize(
        ResolvedStyle style,
        Length? value,
        float fallback) {
        if (value is null) {
            return fallback;
        }

        style.UsesViewportUnits |= value.Value.UsesViewportUnits;
        return Math.Max(
            1,
            value.Value.Resolve(
                _metrics,
                _metrics.ViewportWidth,
                fallback,
                emSize: fallback));
    }

    private float ResolveBlurRadius(
        ResolvedStyle style,
        BlurFilter? value,
        float fallback) {
        if (value is null) {
            return fallback;
        }

        style.UsesViewportUnits |= value.Value.Radius.UsesViewportUnits;
        return Math.Max(
            0,
            value.Value.Radius.Resolve(
                _metrics,
                _metrics.ViewportWidth,
                fallback,
                emSize: style.FontSize));
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
}
