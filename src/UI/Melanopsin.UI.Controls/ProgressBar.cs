using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class ProgressBar : Control, IRenderCommandProvider {
    private Color _accentColor = new(32, 112, 90);

    public ProgressBar()
        : base("ProgressBar") {
        IsRepaintBoundary = true;
        Accessibility.Role = AccessibilityRole.ProgressBar;
    }

    [MarkupAttribute("value", MarkupValueKind.Number)]
    public double Value { get; private set; }

    [MarkupAttribute("maximum", MarkupValueKind.Number)]
    public double Maximum { get; private set; } = 100;

    public Color AccentColor {
        get => _accentColor;
        set {
            if (_accentColor == value) {
                return;
            }

            _accentColor = value;
            InvalidatePaint();
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var trackHeight = Math.Max(3, context.FontSize * 0.3f);
        var trackY = context.Bounds.MinY + ((context.Bounds.Height - trackHeight) / 2);
        var width = Math.Max(0, context.Bounds.Width - (context.Padding * 2));
        var progress = Maximum <= 0
            ? 0
            : (float)Math.Clamp(Value / Maximum, 0, 1);
        commands.Add(new FillRectangleCommand(
            new Rect(context.Bounds.MinX + context.Padding, trackY, width, trackHeight),
            new Color(205, 216, 211),
            trackHeight / 2));
        commands.Add(new FillRectangleCommand(
            new Rect(
                context.Bounds.MinX + context.Padding,
                trackY,
                width * progress,
                trackHeight),
            AccentColor,
            trackHeight / 2));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "value":
                Value = value is float progress ? progress : Value;
                break;
            case "maximum":
                Maximum = value is float maximum ? maximum : Maximum;
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name is "value" or "maximum"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }
}
