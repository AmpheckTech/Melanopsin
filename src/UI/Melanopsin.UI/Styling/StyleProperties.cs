// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Motion;

namespace Melanopsin.UI.Styling;

/// <summary>
/// A typed style declaration set. Properties left as <see langword="null"/>
/// are not specified and retain the value produced by earlier cascade rules.
/// </summary>
public sealed class StyleProperties {
    public Color? Background { get; set; }

    public Color? Foreground { get; set; }

    public Color? BorderColor { get; set; }

    public Length? BorderWidth { get; set; }

    public Length? BorderRadius { get; set; }

    public Length? Padding { get; set; }

    public Length? Margin { get; set; }

    public Length? Width { get; set; }

    public Length? Height { get; set; }

    public Length? MinWidth { get; set; }

    public Length? MaxWidth { get; set; }

    public Length? MinHeight { get; set; }

    public Length? MaxHeight { get; set; }

    public float? FlexGrow { get; set; }

    public float? FlexShrink { get; set; }

    public float? Weight { get; set; }

    public int? Order { get; set; }

    public int? GridRow { get; set; }

    public int? GridColumn { get; set; }

    public int? GridRowSpan { get; set; }

    public int? GridColumnSpan { get; set; }

    public LayoutAlignment? AlignSelf { get; set; }

    public Anchor? Anchor { get; set; }

    public Length? FontSize { get; set; }

    public TextHorizontalAlignment? TextHorizontalAlignment { get; set; }

    public TextVerticalAlignment? TextVerticalAlignment { get; set; }

    public float? Opacity { get; set; }

    /// <summary>
    /// Applies a blur to this element and its descendants after they have been
    /// composited into an isolated layer.
    /// </summary>
    public BlurFilter? Filter { get; set; }

    /// <summary>
    /// Applies a blur to content already painted behind this element. The
    /// element itself is then painted above the filtered backdrop.
    /// </summary>
    public BlurFilter? BackdropFilter { get; set; }

    public UiDuration? TransitionDuration { get; set; }

    public Easing? TransitionEasing { get; set; }

    public CursorKind? Cursor { get; set; }

    public Color? ScrollbarThumbColor { get; set; }

    public Color? ScrollbarTrackColor { get; set; }

    public Length? ScrollbarThickness { get; set; }

    public Length? ScrollbarMinLength { get; set; }

    public Length? ScrollbarInset { get; set; }

    internal bool AffectsLayout =>
        Padding is not null ||
        Margin is not null ||
        Width is not null ||
        Height is not null ||
        MinWidth is not null ||
        MaxWidth is not null ||
        MinHeight is not null ||
        MaxHeight is not null ||
        FlexGrow is not null ||
        FlexShrink is not null ||
        Weight is not null ||
        Order is not null ||
        GridRow is not null ||
        GridColumn is not null ||
        GridRowSpan is not null ||
        GridColumnSpan is not null ||
        AlignSelf is not null ||
        Anchor is not null ||
        FontSize is not null;
}
