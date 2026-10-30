using System.Text;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupElement]
public sealed class RichText :
    Control,
    IRenderCommandProvider,
    ICustomTextMeasurer {
    private readonly List<RichTextSpan> _spans = [];

    public RichText()
        : base("RichText") {
        Accessibility.Role = AccessibilityRole.Text;
    }

    public IReadOnlyList<RichTextSpan> Spans => _spans;

    public void AddSpan(RichTextSpan span) {
        ArgumentNullException.ThrowIfNull(span);
        _spans.Add(span);
        Accessibility.Name = _spans.Aggregate(
            new StringBuilder(),
            static (builder, current) => builder.Append(current.Text))
            .ToString();
        InvalidateLayout();
    }

    public void ClearSpans() {
        if (_spans.Count == 0) {
            return;
        }

        _spans.Clear();
        Accessibility.Name = null;
        InvalidateLayout();
    }

    public TextMetrics MeasureTextLayout(
        ITextLayout textLayout,
        float maxWidth,
        float defaultFontSize,
        FontFace? defaultFont = null) {
        var layout = LayoutSpans(
            textLayout,
            maxWidth,
            defaultFontSize,
            defaultFont,
            ForegroundColor);
        return new TextMetrics(
            layout.Width,
            layout.Height,
            layout.Ascent,
            layout.Descent);
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (context.TextLayout is not { } textLayout) {
            context.AppendText(
                commands,
                string.Concat(Spans.Select(static span => span.Text)),
                context.Bounds,
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center);
            return;
        }

        var layout = LayoutSpans(
            textLayout,
            Math.Max(1, context.Bounds.Width - (context.Padding * 2)),
            context.FontSize,
            context.Font,
            context.Foreground);
        var top = context.Bounds.Y +
            Math.Max(0, (context.Bounds.Height - layout.Height) / 2);
        foreach (var line in layout.Lines) {
            var x = context.Bounds.X + context.Padding;
            foreach (var run in line.Run) {
                var runWidth = Math.Max(0, run.Run.Width);
                commands.Add(new DrawGlyphRunCommand(
                    new Rect(x, top, runWidth, Math.Max(0, line.Height)),
                    run.Run,
                    top + line.Ascent,
                    run.Color,
                    x));
                x += runWidth;
            }

            top += line.Height;
        }
    }

    private RichLayout LayoutSpans(
        ITextLayout textLayout,
        float maxWidth,
        float defaultFontSize,
        FontFace? defaultFont,
        Color defaultColor) {
        var lines = new List<RichLine>();
        var runs = new List<ColoredRun>();
        var width = 0f;
        var ascent = 0f;
        var descent = 0f;

        foreach (var span in Spans) {
            var fontSize = span.FontSize ?? defaultFontSize;
            var font = span.Font ?? defaultFont;
            var shaped = textLayout.Layout(
                span.Text,
                new TextLayoutOptions(fontSize, maxWidth, font: font));
            for (var lineIndex = 0; lineIndex < shaped.Lines.Count; lineIndex++) {
                var line = shaped.Lines[lineIndex];
                if (width > 0 && width + line.Width > maxWidth) {
                    FlushLine();
                }

                foreach (var run in line.Runs) {
                    runs.Add(new ColoredRun(
                        run.Offset(width),
                        span.Color ?? defaultColor));
                }

                width += line.Width;
                ascent = Math.Max(ascent, line.Ascent);
                descent = Math.Max(descent, line.Descent);
                if (lineIndex < shaped.Lines.Count - 1) {
                    FlushLine();
                }
            }
        }

        FlushLine();
        if (lines.Count == 0) {
            return new RichLayout([], 0, 0, 0, 0);
        }

        return new RichLayout(
            lines,
            lines.Max(static line => line.Width),
            lines.Sum(static line => line.Height),
            lines.Max(static line => line.Ascent),
            lines.Max(static line => line.Descent));

        void FlushLine() {
            if (runs.Count == 0 && lines.Count == 0) {
                return;
            }

            lines.Add(new RichLine(
                [.. runs],
                width,
                Math.Max(1, ascent + descent),
                ascent,
                descent));
            runs.Clear();
            width = 0;
            ascent = 0;
            descent = 0;
        }
    }

    private Color ForegroundColor => new(28, 37, 34);

}
