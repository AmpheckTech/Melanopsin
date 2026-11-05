// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using Melanopsin.Graphics.Text;
using SkiaSharp;
using static Melanopsin.Graphics.Backend.Skia.SkiaPrimitiveConversions;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaTextRenderer {
    public void DrawEditableText(
        SKCanvas canvas,
        DrawEditableTextCommand command) {
        if (GetLineTypeface(command.Font) is null) {
            return;
        }

        canvas.Save();
        canvas.ClipRect(ToSkRect(command.Bounds));
        var paint = _textPaint;
        paint.IsAntialias = true;
        var (displayText, caret, selectionStart, selectionLength) =
            PrepareEditableText(command);
        if (displayText.Length == 0) {
            DrawTextAt(
                canvas,
                command.Placeholder ?? string.Empty,
                command.Bounds.MinX,
                command.Bounds,
                command.FontSize,
                command.PlaceholderColor,
                paint,
                command.Font);
            canvas.Restore();
            return;
        }

        var caretX = MeasureWidth(
            displayText.AsSpan(0, caret),
            command.FontSize,
            paint,
            command.Font);
        var scrollX = Math.Max(0, caretX - command.Bounds.Width);
        var lineHeight = EditableLineHeight(
            command.Bounds,
            command.FontSize,
            command.Font);
        var lineTop = EditableLineTop(command.Bounds, lineHeight);
        if (selectionLength > 0) {
            var start = Math.Clamp(selectionStart, 0, displayText.Length);
            var end = Math.Clamp(
                selectionStart + selectionLength,
                start,
                displayText.Length);
            var startX = MeasureWidth(
                displayText.AsSpan(0, start),
                command.FontSize,
                paint,
                command.Font);
            var endX = MeasureWidth(
                displayText.AsSpan(0, end),
                command.FontSize,
                paint,
                command.Font);
            var selectionPaint = _selectionPaint;
            selectionPaint.Color = ToSkColor(command.SelectionColor);
            selectionPaint.IsAntialias = false;
            canvas.DrawRect(
                new SKRect(
                    command.Bounds.MinX + startX - scrollX,
                    lineTop,
                    command.Bounds.MinX + endX - scrollX,
                    lineTop + lineHeight),
                selectionPaint);
        }

        DrawTextAt(
            canvas,
            displayText,
            command.Bounds.MinX - scrollX,
            command.Bounds,
            command.FontSize,
            command.Color,
            paint,
            command.Font);
        if (command.ShowCaret) {
            var caretPaint = _caretPaint;
            caretPaint.Color = ToSkColor(command.CaretColor);
            caretPaint.IsAntialias = false;
            var caretLeft = Math.Clamp(
                command.Bounds.MinX + caretX - scrollX,
                command.Bounds.MinX,
                Math.Max(command.Bounds.MinX, command.Bounds.MaxX - 1));
            canvas.DrawRect(
                new SKRect(
                    caretLeft,
                    lineTop,
                    caretLeft + 1,
                    lineTop + lineHeight),
                caretPaint);
        }

        canvas.Restore();
    }

    public void DrawShapedEditableText(
        SKCanvas canvas,
        DrawShapedEditableTextCommand command) {
        canvas.Save();
        canvas.ClipRect(ToSkRect(command.Bounds));
        var shaped = command.Text.Text.Length == 0
            ? command.Placeholder
            : command.Text;
        var color = command.Text.Text.Length == 0
            ? command.PlaceholderColor
            : command.Color;
        var line = command.Text.Lines.Count == 0
            ? null
            : command.Text.Lines[0];
        var lineHeight = line is null
            ? command.Bounds.Height
            : Math.Min(command.Bounds.Height, line.Height);
        var lineTop = EditableLineTop(command.Bounds, lineHeight);
        if (shaped is not null) {
            var shapedLine = shaped.Lines.Count == 0 ? null : shaped.Lines[0];
            if (shapedLine is not null) {
                if (command.SelectionEndX > command.SelectionStartX) {
                    var selectionPaint = _selectionPaint;
                    selectionPaint.Color = ToSkColor(command.SelectionColor);
                    selectionPaint.IsAntialias = false;
                    canvas.DrawRect(
                        new SKRect(
                            command.Bounds.MinX + command.SelectionStartX - command.ScrollX,
                            lineTop,
                            command.Bounds.MinX + command.SelectionEndX - command.ScrollX,
                            lineTop + lineHeight),
                        selectionPaint);
                }

                var baseline = lineTop + shapedLine.Ascent;
                var paint = _textPaint;
                paint.Color = ToSkColor(color);
                paint.IsAntialias = true;
                foreach (var run in shapedLine.Runs) {
                    var runCommand = new DrawGlyphRunCommand(
                        command.Bounds,
                        run,
                        baseline,
                        color,
                        command.Bounds.MinX - command.ScrollX);
                    DrawGlyphRun(canvas, runCommand);
                }
            }
        }

        if (command.ShowCaret) {
            var caretPaint = _caretPaint;
            caretPaint.Color = ToSkColor(command.CaretColor);
            caretPaint.IsAntialias = false;
            var caretLeft = Math.Clamp(
                command.Bounds.MinX + command.CaretX - command.ScrollX,
                command.Bounds.MinX,
                Math.Max(command.Bounds.MinX, command.Bounds.MaxX - 1));
            canvas.DrawRect(
                new SKRect(
                    caretLeft,
                    lineTop,
                    caretLeft + 1,
                    lineTop + lineHeight),
                caretPaint);
        }

        canvas.Restore();
    }

    private static (string Text, int Caret, int SelectionStart, int SelectionLength)
        PrepareEditableText(DrawEditableTextCommand command) {
        if (!command.IsPassword || command.Text.Length == 0) {
            return (
                command.Text,
                Math.Clamp(command.CaretIndex, 0, command.Text.Length),
                command.SelectionStart,
                command.SelectionLength);
        }

        var characters = StringInfo.ParseCombiningCharacters(command.Text);
        var mask = new string('•', characters.Length);
        return (
            mask,
            PasswordIndex(characters, command.CaretIndex),
            PasswordIndex(characters, command.SelectionStart),
            PasswordIndex(
                characters,
                command.SelectionStart + command.SelectionLength) -
            PasswordIndex(characters, command.SelectionStart));
    }

    private static int PasswordIndex(
        IReadOnlyList<int> characterStarts,
        int utf16Index) {
        var count = 0;
        foreach (var start in characterStarts) {
            if (start >= utf16Index) {
                break;
            }

            count++;
        }

        return count;
    }

    private float MeasureWidth(
        ReadOnlySpan<char> text,
        float fontSize,
        SKPaint paint,
        FontFace? font) {
        var width = 0f;
        foreach (var run in _fontCache.CreateTextRuns(text.ToString(), font)) {
            var runFont = _fontCache.GetFont(run.Typeface, fontSize);
            width += runFont.MeasureText(
                text.Slice(run.Start, run.Length),
                paint);
        }

        return width;
    }

    private void DrawTextAt(
        SKCanvas canvas,
        string text,
        float x,
        Rect bounds,
        float fontSize,
        Color color,
        SKPaint paint,
        FontFace? font) {
        if (text.Length == 0) {
            return;
        }

        var lineTypeface = GetLineTypeface(font)!;
        var lineFont = _fontCache.GetFont(lineTypeface, fontSize);
        var metrics = lineFont.Metrics;
        var baseline = bounds.MinY +
            ((bounds.Height - (metrics.Descent - metrics.Ascent)) / 2) -
            metrics.Ascent;
        paint.Color = ToSkColor(color);
        foreach (var run in _fontCache.CreateTextRuns(text, font)) {
            var runFont = _fontCache.GetFont(run.Typeface, fontSize);
            canvas.DrawText(
                text.Substring(run.Start, run.Length),
                x,
                baseline,
                SKTextAlign.Left,
                runFont,
                paint);
            x += runFont.MeasureText(
                text.AsSpan(run.Start, run.Length),
                paint);
        }
    }

    private float EditableLineHeight(
        Rect bounds,
        float fontSize,
        FontFace? font) {
        var lineTypeface = GetLineTypeface(font)!;
        var metrics = _fontCache.GetFont(lineTypeface, fontSize).Metrics;
        return Math.Min(
            bounds.Height,
            Math.Max(0, metrics.Descent - metrics.Ascent));
    }

    private static float EditableLineTop(Rect bounds, float lineHeight) {
        return bounds.MinY + ((bounds.Height - lineHeight) / 2);
    }
}
