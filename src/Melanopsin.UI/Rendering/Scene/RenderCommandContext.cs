namespace Melanopsin.UI.Rendering;

public readonly record struct RenderCommandContext {
    public RenderCommandContext(
        Rect bounds,
        float cornerRadius,
        Color foreground,
        float fontSize,
        float padding,
        FontFace? font = null,
        ITextLayout? textLayout = null) {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(fontSize);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);
        Bounds = bounds;
        CornerRadius = cornerRadius;
        Foreground = foreground;
        FontSize = fontSize;
        Padding = padding;
        Font = font;
        TextLayout = textLayout;
    }

    public Rect Bounds { get; }

    public float CornerRadius { get; }

    public Color Foreground { get; }

    public float FontSize { get; }

    public float Padding { get; }

    public FontFace? Font { get; }

    public ITextLayout? TextLayout { get; }

    public void AppendText(
        List<RenderCommand> commands,
        string text,
        Rect bounds,
        Color color,
        TextHorizontalAlignment horizontalAlignment,
        TextVerticalAlignment verticalAlignment,
        bool wrap = true) {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) {
            return;
        }

        if (TextLayout is null) {
            commands.Add(new DrawTextCommand(
                text,
                bounds,
                color,
                FontSize,
                horizontalAlignment,
                verticalAlignment,
                Font));
            return;
        }

        var displayText = wrap
            ? text
            : TextFitting.FitSingleLine(
                TextLayout,
                text,
                FontSize,
                Font,
                bounds.Width);
        var shaped = TextLayout.Layout(
            displayText,
            new TextLayoutOptions(
                FontSize,
                wrap ? Math.Max(1, bounds.Width) : float.PositiveInfinity,
                font: Font));
        var top = verticalAlignment switch {
            TextVerticalAlignment.Top => bounds.Y,
            TextVerticalAlignment.Bottom => bounds.Bottom - shaped.Height,
            _ => bounds.Y + ((bounds.Height - shaped.Height) / 2),
        };
        foreach (var line in shaped.Lines) {
            var lineWidth = Math.Max(0, line.Width);
            var lineX = horizontalAlignment switch {
                TextHorizontalAlignment.Center => (bounds.Width - lineWidth) / 2,
                TextHorizontalAlignment.End => bounds.Width - lineWidth,
                _ => 0,
            };
            foreach (var run in line.Runs) {
                commands.Add(new DrawGlyphRunCommand(
                    new Rect(
                        bounds.X + lineX,
                        top,
                        lineWidth,
                        line.Height),
                    run,
                    top + line.Ascent,
                    color,
                    bounds.X + lineX));
            }

            top += line.Height;
        }
    }
}
