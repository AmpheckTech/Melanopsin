using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

internal static class ResolvedStyleComparer {
    public static bool AreEquivalent(ResolvedStyle left, ResolvedStyle right) {
        return left.Background == right.Background &&
               left.Foreground == right.Foreground &&
               left.BorderColor == right.BorderColor &&
               left.BorderWidth == right.BorderWidth &&
               left.BorderRadius == right.BorderRadius &&
               left.Padding == right.Padding &&
               left.Margin == right.Margin &&
               left.Width == right.Width &&
               left.Height == right.Height &&
               left.MinWidth == right.MinWidth &&
               left.MaxWidth == right.MaxWidth &&
               left.MinHeight == right.MinHeight &&
               left.MaxHeight == right.MaxHeight &&
               left.FlexGrow == right.FlexGrow &&
               left.FlexShrink == right.FlexShrink &&
               left.Weight == right.Weight &&
               left.Order == right.Order &&
               left.GridRow == right.GridRow &&
               left.GridColumn == right.GridColumn &&
               left.GridRowSpan == right.GridRowSpan &&
               left.GridColumnSpan == right.GridColumnSpan &&
               left.AlignSelf == right.AlignSelf &&
               ReferenceEquals(left.Font, right.Font) &&
               left.FontSize == right.FontSize &&
               left.TextHorizontalAlignment == right.TextHorizontalAlignment &&
               left.TextVerticalAlignment == right.TextVerticalAlignment &&
               left.Opacity == right.Opacity &&
               left.FilterBlurRadius == right.FilterBlurRadius &&
               left.BackdropBlurRadius == right.BackdropBlurRadius &&
               left.TransitionDuration == right.TransitionDuration &&
               left.TransitionEasing == right.TransitionEasing &&
               left.Cursor == right.Cursor &&
               left.ScrollbarThumbColor == right.ScrollbarThumbColor &&
               left.ScrollbarTrackColor == right.ScrollbarTrackColor &&
               left.ScrollbarThickness == right.ScrollbarThickness &&
               left.ScrollbarMinLength == right.ScrollbarMinLength &&
               left.ScrollbarInset == right.ScrollbarInset &&
               left.Anchor == right.Anchor &&
               left.UsesViewportUnits == right.UsesViewportUnits;
    }

    public static InvalidationKind GetInvalidationKind(
        ResolvedStyle before,
        ResolvedStyle after) {
        var paint = before.Background != after.Background ||
            before.Foreground != after.Foreground ||
            before.BorderColor != after.BorderColor ||
            before.BorderWidth != after.BorderWidth ||
            before.BorderRadius != after.BorderRadius ||
            before.TextHorizontalAlignment != after.TextHorizontalAlignment ||
            before.TextVerticalAlignment != after.TextVerticalAlignment ||
            before.Opacity != after.Opacity ||
            before.FilterBlurRadius != after.FilterBlurRadius ||
            before.BackdropBlurRadius != after.BackdropBlurRadius;
        paint |= before.ScrollbarThumbColor != after.ScrollbarThumbColor ||
            before.ScrollbarTrackColor != after.ScrollbarTrackColor ||
            before.ScrollbarThickness != after.ScrollbarThickness ||
            before.ScrollbarMinLength != after.ScrollbarMinLength ||
            before.ScrollbarInset != after.ScrollbarInset;
        var layout = before.Padding != after.Padding ||
            before.Margin != after.Margin ||
            before.Width != after.Width ||
            before.Height != after.Height ||
            before.MinWidth != after.MinWidth ||
            before.MaxWidth != after.MaxWidth ||
            before.MinHeight != after.MinHeight ||
            before.MaxHeight != after.MaxHeight ||
            before.FlexGrow != after.FlexGrow ||
            before.FlexShrink != after.FlexShrink ||
            before.Weight != after.Weight ||
            before.Order != after.Order ||
            before.GridRow != after.GridRow ||
            before.GridColumn != after.GridColumn ||
            before.GridRowSpan != after.GridRowSpan ||
            before.GridColumnSpan != after.GridColumnSpan ||
            before.AlignSelf != after.AlignSelf ||
            before.Anchor != after.Anchor ||
            before.ScrollbarThickness != after.ScrollbarThickness ||
            before.ScrollbarInset != after.ScrollbarInset ||
            before.UsesViewportUnits != after.UsesViewportUnits ||
            !ReferenceEquals(before.Font, after.Font) ||
            before.FontSize != after.FontSize;
        if (layout) {
            return InvalidationKind.LayoutAndPaint;
        }

        return paint ? InvalidationKind.Paint : InvalidationKind.None;
    }

}
