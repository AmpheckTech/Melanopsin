using System.Text;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupElement]
[MarkupAttribute("spans")]
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
        UpdateAccessibilityName();
        InvalidateLayout();
    }

    /// <summary>
    /// Replaces every span. Markup binds this through the <c>spans</c> attribute,
    /// so a component can describe rich text as data instead of building it
    /// imperatively.
    /// </summary>
    public void SetSpans(IReadOnlyList<RichTextSpan> spans) {
        ArgumentNullException.ThrowIfNull(spans);
        _spans.Clear();
        _spans.AddRange(spans);
        UpdateAccessibilityName();
        InvalidateLayout();
    }

    public void ClearSpans() {
        if (_spans.Count == 0) {
            return;
        }

        _spans.Clear();
        UpdateAccessibilityName();
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
        // Runs carry their offset inside the line, so the command origin stays
        // at the line start; adding the running x on top would double count it.
        var lineOrigin = context.Bounds.X + context.Padding;
        foreach (var line in layout.Lines) {
            var x = lineOrigin;
            foreach (var run in line.Run) {
                var runWidth = Math.Max(0, run.Run.Width);
                commands.Add(new DrawGlyphRunCommand(
                    new Rect(x, top, runWidth, Math.Max(0, line.Height)),
                    run.Run,
                    top + line.Ascent,
                    run.Color,
                    lineOrigin));
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
        var available = Math.Max(1, maxWidth);
        var lines = new List<RichLine>();
        var runs = new List<ColoredRun>();
        var width = 0f;
        var ascent = 0f;
        var descent = 0f;

        foreach (var span in Spans) {
            var text = span.Text;
            if (text.Length == 0) {
                continue;
            }

            var fontSize = span.FontSize ?? defaultFontSize;
            var font = span.Font ?? defaultFont;
            var color = span.Color ?? defaultColor;
            var offset = 0;
            while (offset < text.Length) {
                // Reshape against the space left on the current line so a span
                // continues an earlier one instead of always starting a new line.
                var shaped = textLayout.Layout(
                    text[offset..],
                    new TextLayoutOptions(
                        fontSize,
                        width > 0 ? Math.Max(1, available - width) : available,
                        font: font));
                var line = shaped.Lines[0];
                if (width > 0 && width + line.Width > available) {
                    FlushLine();
                    continue;
                }

                foreach (var run in line.Runs) {
                    runs.Add(new ColoredRun(run.Offset(width), color));
                }

                width += line.Width;
                ascent = Math.Max(ascent, line.Ascent);
                descent = Math.Max(descent, line.Descent);
                var consumed = LineLength(line);
                if (consumed <= 0) {
                    // An empty line can only come from a hard break: skip it.
                    consumed = 1;
                }

                offset += consumed;
                if (offset < text.Length) {
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
            if (runs.Count == 0) {
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

    /// <summary>Logical length of the first line a shaped layout produced.</summary>
    private static int LineLength(TextLine line) {
        var end = 0;
        foreach (var run in line.Runs) {
            end = Math.Max(end, run.Start + run.Length);
        }

        return end;
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "spans" && value is IReadOnlyList<RichTextSpan> spans) {
            SetSpans(spans);
        }
    }

    private void UpdateAccessibilityName() {
        Accessibility.Name = _spans.Count == 0
            ? null
            : _spans.Aggregate(
                new StringBuilder(),
                static (builder, span) => builder.Append(span.Text))
                .ToString();
    }

    private Color ForegroundColor => new(28, 37, 34);

}
