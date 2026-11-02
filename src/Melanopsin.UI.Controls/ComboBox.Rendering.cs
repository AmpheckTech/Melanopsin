using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

public sealed partial class ComboBox {
    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var arrowWidth = context.FontSize;
        var textBounds = new Rect(
            context.Bounds.X + (context.Padding * 2),
            context.Bounds.Y,
            Math.Max(
                0,
                context.Bounds.Width - (context.Padding * 4) - arrowWidth),
            context.Bounds.Height);
        var displayText = context.TextLayout is { } textLayout
            ? TextFitting.FitSingleLine(
                textLayout,
                SelectedText,
                context.FontSize,
                context.Font,
                textBounds.Width)
            : SelectedText;
        context.AppendText(
            commands,
            displayText,
            textBounds,
            context.Foreground,
            TextHorizontalAlignment.Start,
            TextVerticalAlignment.Center,
            wrap: false);

        var arrowBounds = new Rect(
            context.Bounds.Right - context.Padding - arrowWidth,
            context.Bounds.Y,
            arrowWidth,
            context.Bounds.Height);
        context.AppendText(
            commands,
            IsOpen ? "▴" : "▾",
            arrowBounds,
            context.Foreground,
            TextHorizontalAlignment.Center,
            TextVerticalAlignment.Center,
            wrap: false);
    }
}
