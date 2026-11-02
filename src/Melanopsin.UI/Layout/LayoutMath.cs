namespace Melanopsin.UI.Layout;

internal static class LayoutMath {
    public static float ResolveGap(Length gap, UiMetrics metrics) {
        return gap.Resolve(metrics, available: 0, fallback: metrics.Space(1));
    }

    public static float AlignX(
        Rect content,
        float width,
        Anchor anchor,
        float margin) {
        return anchor switch {
            Anchor.Top or Anchor.Center or Anchor.Bottom =>
                content.X + ((content.Width - width) / 2),
            Anchor.TopRight or Anchor.Right or Anchor.BottomRight =>
                content.Right - width - margin,
            _ => content.X + margin,
        };
    }

    public static float AlignY(
        Rect content,
        float height,
        Anchor anchor,
        float margin) {
        return anchor switch {
            Anchor.Left or Anchor.Center or Anchor.Right =>
                content.Y + ((content.Height - height) / 2),
            Anchor.BottomLeft or Anchor.Bottom or Anchor.BottomRight =>
                content.Bottom - height - margin,
            _ => content.Y + margin,
        };
    }
}
