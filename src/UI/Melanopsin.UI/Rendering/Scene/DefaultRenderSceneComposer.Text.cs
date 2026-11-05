// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Rendering;

public sealed partial class DefaultRenderSceneComposer {
    private void AppendTextNode(
        List<RenderCommand> commands,
        TextNode text,
        Rect bounds) {
        var fontSize = Math.Max(
            _styleEngine.Metrics.Font(10),
            Math.Min(
                _styleEngine.Metrics.Font(UiMetrics.DefaultFontSize),
                bounds.Height - _styleEngine.Metrics.Space(1)));
        if (_textLayout is not null) {
            var shaped = _textLayout.Layout(
                text.Text,
                new TextLayoutOptions(fontSize, Math.Max(1, bounds.Width)));
            AppendShapedText(
                commands,
                shaped,
                bounds,
                new Color(232, 238, 242),
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center);
        } else {
            commands.Add(new DrawTextCommand(
                text.Text,
                bounds,
                new Color(232, 238, 242),
                fontSize,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center));
        }
    }

    private void AppendControlText(
        List<RenderCommand> commands,
        ITextContentControl control,
        Rect bounds,
        ResolvedStyle style,
        bool wrap) {
        var textBounds = new Rect(
            bounds.MinX + style.Padding,
            bounds.MinY,
            Math.Max(1, bounds.Width - (style.Padding * 2)),
            bounds.Height);
        var fontSize = Math.Max(
            _styleEngine.Metrics.Font(10),
            Math.Min(
                style.FontSize,
                bounds.Height - _styleEngine.Metrics.Space(1)));
        if (_textLayout is not null) {
            var displayText = wrap
                ? control.Text
                : TextFitting.FitSingleLine(
                    _textLayout,
                    control.Text,
                    fontSize,
                    style.Font,
                    textBounds.Width);
            var shaped = _textLayout.Layout(
                displayText,
                new TextLayoutOptions(
                    fontSize,
                    wrap ? textBounds.Width : float.PositiveInfinity,
                    font: style.Font));
            AppendShapedText(
                commands,
                shaped,
                textBounds,
                ApplyOpacity(style.Foreground, style.Opacity),
                style.TextHorizontalAlignment,
                style.TextVerticalAlignment);
        } else {
            commands.Add(new DrawTextCommand(
                control.Text,
                textBounds,
                ApplyOpacity(style.Foreground, style.Opacity),
                fontSize,
                style.TextHorizontalAlignment,
                style.TextVerticalAlignment,
                style.Font));
        }
    }

    private static void AppendShapedText(
        List<RenderCommand> commands,
        ShapedText text,
        Rect bounds,
        Color color,
        TextHorizontalAlignment horizontalAlignment,
        TextVerticalAlignment verticalAlignment) {
        var top = verticalAlignment switch {
            TextVerticalAlignment.Top => bounds.MinY,
            TextVerticalAlignment.Bottom => bounds.MaxY - text.Height,
            _ => bounds.MinY + ((bounds.Height - text.Height) / 2),
        };
        foreach (var line in text.Lines) {
            var lineWidth = Math.Max(0, line.Width);
            var lineX = horizontalAlignment switch {
                TextHorizontalAlignment.Center => (bounds.Width - lineWidth) / 2,
                TextHorizontalAlignment.End => bounds.Width - lineWidth,
                _ => 0,
            };
            foreach (var run in line.Runs) {
                commands.Add(new DrawGlyphRunCommand(
                    new Rect(
                        bounds.MinX + lineX,
                        top,
                        lineWidth,
                        line.Height),
                    run,
                    top + line.Ascent,
                    color,
                    bounds.MinX + lineX));
            }

            top += line.Height;
        }
    }
}
