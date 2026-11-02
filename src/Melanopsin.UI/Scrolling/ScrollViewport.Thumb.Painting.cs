using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

public sealed partial class ScrollViewport {
    private const float IdleThumbVisibility = 0.62f;
    private const float ThumbFadeSpeed = 18f;
    private static readonly Color DefaultThumbColor = new(126, 136, 146, 190);

    private float _thumbVisibility = IdleThumbVisibility;

    public void AppendHitRegionBounds(List<Rect> bounds) {
        ArgumentNullException.ThrowIfNull(bounds);
        if (TryGetThumbBar(ScrollAxis.Vertical, out _, out _) &&
            TryGetTrackHitArea(ScrollAxis.Vertical, out var verticalHitArea)) {
            bounds.Add(verticalHitArea);
        }

        if (TryGetThumbBar(ScrollAxis.Horizontal, out _, out _) &&
            TryGetTrackHitArea(ScrollAxis.Horizontal, out var horizontalHitArea)) {
            bounds.Add(horizontalHitArea);
        }
    }

    public void AppendOverlayRenderCommands(
        RenderCommandContext context,
        ResolvedStyle style,
        List<RenderCommand> commands) {
        UpdateScrollbarMetrics(style);
        if (context.Styles is { } styles) {
            RefreshScrollbarStyles(style, styles);
        }

        var visibility = Math.Clamp(_thumbVisibility, 0, 1);
        if (visibility <= 0.001f) {
            return;
        }

        var trackStyle = GetTrackStyle(style);
        if (TryGetThumbBar(ScrollAxis.Vertical, out var verticalBar, out _) &&
            TryGetTrack(ScrollAxis.Vertical, out var verticalTrack)) {
            AppendScrollbarRectangle(
                commands,
                verticalTrack,
                trackStyle,
                style,
                visibility);
            AppendScrollbarRectangle(
                commands,
                verticalBar.Thumb,
                GetThumbStyle(ScrollAxis.Vertical, style),
                style,
                visibility);
        }

        if (TryGetThumbBar(ScrollAxis.Horizontal, out var horizontalBar, out _) &&
            TryGetTrack(ScrollAxis.Horizontal, out var horizontalTrack)) {
            AppendScrollbarRectangle(
                commands,
                horizontalTrack,
                trackStyle,
                style,
                visibility);
            AppendScrollbarRectangle(
                commands,
                horizontalBar.Thumb,
                GetThumbStyle(ScrollAxis.Horizontal, style),
                style,
                visibility);
        }
    }

    private void UpdateScrollbarMetrics(ResolvedStyle style) {
        _scrollbarThickness = style.ScrollbarThickness;
        _scrollbarMinLength = style.ScrollbarMinLength;
        _scrollbarInset = style.ScrollbarInset;
    }

    private bool AdvanceThumbAnimation(float elapsedSeconds) {
        var target = _pointerInScrollbar || _thumbDragging
            ? 1f
            : IdleThumbVisibility;
        var amount = 1f - MathF.Exp(
            -ThumbFadeSpeed * Math.Clamp(elapsedSeconds, 0, 0.1f));
        var next = _thumbVisibility + ((target - _thumbVisibility) * amount);
        if (Math.Abs(next - _thumbVisibility) <= 0.001f) {
            return false;
        }

        _thumbVisibility = next;
        return true;
    }

    private static Color ApplyVisibility(
        Color color,
        float opacity,
        float visibility) {
        return new Color(
            color.Red,
            color.Green,
            color.Blue,
            (byte)Math.Clamp(
                color.Alpha * opacity * visibility,
                0,
                255));
    }
}
