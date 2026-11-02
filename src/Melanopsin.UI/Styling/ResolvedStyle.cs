using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

public sealed class ResolvedStyle {
    internal ResolvedStyle() {
    }

    public Color Background { get; internal set; } = new(0, 0, 0, 0);

    public Color Foreground { get; internal set; } = new(255, 255, 255);

    public Color BorderColor { get; internal set; } = new(0, 0, 0, 0);

    public float BorderWidth { get; internal set; }

    public float BorderRadius { get; internal set; }

    public float Padding { get; internal set; } = 8;

    public float Margin { get; internal set; }

    public Length Width { get; internal set; } = Length.Auto;

    public Length Height { get; internal set; } = Length.Auto;

    public Length? MinWidth { get; internal set; }

    public Length? MaxWidth { get; internal set; }

    public Length? MinHeight { get; internal set; }

    public Length? MaxHeight { get; internal set; }

    public float FlexGrow { get; internal set; }

    public float FlexShrink { get; internal set; } = 1;

    public float Weight { get; internal set; }

    public int Order { get; internal set; }

    public int GridRow { get; internal set; } = -1;

    public int GridColumn { get; internal set; } = -1;

    public int GridRowSpan { get; internal set; } = 1;

    public int GridColumnSpan { get; internal set; } = 1;

    public LayoutAlignment AlignSelf { get; internal set; }

    public Anchor Anchor { get; internal set; } = Anchor.TopLeft;

    public FontFace? Font { get; internal set; }

    public float FontSize { get; internal set; } = 14;

    public TextHorizontalAlignment TextHorizontalAlignment { get; internal set; } =
        TextHorizontalAlignment.Start;

    public TextVerticalAlignment TextVerticalAlignment { get; internal set; } =
        TextVerticalAlignment.Center;

    public float Opacity { get; internal set; } = 1;

    public float FilterBlurRadius { get; internal set; }

    public float BackdropBlurRadius { get; internal set; }

    public float TransitionDuration { get; internal set; } = 0.16f;

    public Easing TransitionEasing { get; internal set; } = Easing.EaseOut;

    /// <summary>
    /// The explicitly declared cursor shape, or <see langword="null"/> when
    /// the shape should be inherited from the nearest styled ancestor.
    /// </summary>
    public CursorKind? Cursor { get; internal set; }

    public Color? ScrollbarThumbColor { get; internal set; }

    public Color? ScrollbarTrackColor { get; internal set; }

    public float ScrollbarThickness { get; internal set; }

    public float ScrollbarMinLength { get; internal set; }

    public float ScrollbarInset { get; internal set; }

    /// <summary>
    /// Whether resolving this style depends on the current viewport size.
    /// This is cache metadata and is intentionally excluded from visual style
    /// comparisons except where cache correctness requires it.
    /// </summary>
    internal bool UsesViewportUnits { get; set; }
}
