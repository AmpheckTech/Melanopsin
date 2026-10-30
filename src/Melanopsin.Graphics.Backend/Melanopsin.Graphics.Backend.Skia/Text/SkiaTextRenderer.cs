using System.Globalization;
using Melanopsin.Graphics.Text;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaTextRenderer : IDisposable {
    private const int MaxGlyphBlobEntries = 2048;
    private readonly SkiaFontCache _fontCache;
    private readonly Dictionary<GlyphBlobKey, SKTextBlob> _glyphBlobs = [];
    private readonly SKPaint _textPaint = new();
    private readonly SKPaint _caretPaint = new();
    private readonly SKPaint _selectionPaint = new();
    private bool _disposed;

    public SkiaTextRenderer(SkiaFontCache fontCache) {
        _fontCache = fontCache;
    }

    public void DrawText(SKCanvas canvas, DrawTextCommand command) {
        var lineTypeface = GetLineTypeface(command.Font);
        if (lineTypeface is null) {
            return;
        }

        canvas.Save();
        canvas.ClipRect(ToSkRect(command.Bounds));
        var font = _fontCache.GetFont(lineTypeface, command.FontSize);
        var paint = _textPaint;
        paint.Color = ToSkColor(command.Color);
        paint.IsAntialias = true;
        var runs = _fontCache.CreateTextRuns(command.Text, command.Font);
        var textWidth = 0f;
        foreach (var run in runs) {
            var runFont = _fontCache.GetFont(run.Typeface, command.FontSize);
            textWidth += runFont.MeasureText(
                command.Text.AsSpan(run.Start, run.Length),
                paint);
        }

        var x = command.HorizontalAlignment switch {
            TextHorizontalAlignment.Center =>
                command.Bounds.X + ((command.Bounds.Width - textWidth) / 2),
            TextHorizontalAlignment.End => command.Bounds.Right - textWidth,
            _ => command.Bounds.X,
        };
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent;
        var y = command.VerticalAlignment switch {
            TextVerticalAlignment.Top => command.Bounds.Y - metrics.Ascent,
            TextVerticalAlignment.Bottom => command.Bounds.Bottom - metrics.Descent,
            _ => command.Bounds.Y +
                 ((command.Bounds.Height - textHeight) / 2) -
                 metrics.Ascent,
        };
        foreach (var run in runs) {
            var runFont = _fontCache.GetFont(run.Typeface, command.FontSize);
            var text = command.Text.Substring(run.Start, run.Length);
            canvas.DrawText(
                text,
                x,
                y,
                SKTextAlign.Left,
                runFont,
                paint);
            x += runFont.MeasureText(text, paint);
        }

        canvas.Restore();
    }

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
                command.Bounds.X,
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
                    command.Bounds.X + startX - scrollX,
                    lineTop,
                    command.Bounds.X + endX - scrollX,
                    lineTop + lineHeight),
                selectionPaint);
        }

        DrawTextAt(
            canvas,
            displayText,
            command.Bounds.X - scrollX,
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
                command.Bounds.X + caretX - scrollX,
                command.Bounds.X,
                Math.Max(command.Bounds.X, command.Bounds.Right - 1));
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

    public void DrawGlyphRun(SKCanvas canvas, DrawGlyphRunCommand command) {
        var typeface = _fontCache.ResolveTypeface(command.Run.Font);
        var font = _fontCache.GetFont(typeface, command.Run.FontSize);
        var paint = _textPaint;
        paint.Color = ToSkColor(command.Color);
        paint.IsAntialias = true;
        var blobKey = new GlyphBlobKey(
            command.Run,
            command.Baseline,
            command.OriginX);
        if (_glyphBlobs.TryGetValue(blobKey, out var cachedBlob)) {
            canvas.DrawText(cachedBlob, 0, 0, paint);
            return;
        }

        var glyphs = new ushort[command.Run.Glyphs.Count];
        var points = new SKPoint[glyphs.Length];
        var x = command.Run.X + command.OriginX;
        for (var index = 0; index < glyphs.Length; index++) {
            var glyph = command.Run.Glyphs[index];
            glyphs[index] = checked((ushort)glyph.Id);
            points[index] = new SKPoint(
                x + glyph.XOffset,
                command.Baseline - glyph.YOffset);
            x += glyph.XAdvance;
        }

        using var builder = new SKTextBlobBuilder();
        builder.AddPositionedRun(glyphs, font, points);
        var blob = builder.Build();
        if (blob is null) {
            return;
        }

        if (_glyphBlobs.Count >= MaxGlyphBlobEntries) {
            Exception? failure = null;
            try {
                foreach (var stale in _glyphBlobs.Values) {
                    SkiaCleanup.Capture(ref failure, stale.Dispose);
                }
            } finally {
                _glyphBlobs.Clear();
            }

            SkiaCleanup.ThrowIfFailed(failure);
        }

        _glyphBlobs[blobKey] = blob;
        canvas.DrawText(blob, 0, 0, paint);
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
                            command.Bounds.X + command.SelectionStartX - command.ScrollX,
                            lineTop,
                            command.Bounds.X + command.SelectionEndX - command.ScrollX,
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
                        command.Bounds.X - command.ScrollX);
                    DrawGlyphRun(canvas, runCommand);
                }
            }
        }

        if (command.ShowCaret) {
            var caretPaint = _caretPaint;
            caretPaint.Color = ToSkColor(command.CaretColor);
            caretPaint.IsAntialias = false;
            var caretLeft = Math.Clamp(
                command.Bounds.X + command.CaretX - command.ScrollX,
                command.Bounds.X,
                Math.Max(command.Bounds.X, command.Bounds.Right - 1));
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

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        SkiaCleanup.Capture(ref failure, _textPaint.Dispose);
        SkiaCleanup.Capture(ref failure, _caretPaint.Dispose);
        SkiaCleanup.Capture(ref failure, _selectionPaint.Dispose);
        try {
            foreach (var blob in _glyphBlobs.Values) {
                SkiaCleanup.Capture(ref failure, blob.Dispose);
            }
        } finally {
            _glyphBlobs.Clear();
        }

        SkiaCleanup.ThrowIfFailed(failure);
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
        var baseline = bounds.Y +
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
        return bounds.Y + ((bounds.Height - lineHeight) / 2);
    }

    private SKTypeface? GetLineTypeface(FontFace? font) {
        return font is null
            ? _fontCache.PrimaryTypeface
            : _fontCache.ResolveTypeface(font);
    }

    private static SKRect ToSkRect(Rect rect) {
        return new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    private static SKColor ToSkColor(Color color) {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
