using Melanopsin.Graphics.Text;
using SkiaSharp;
using static Melanopsin.Graphics.Backend.Skia.SkiaPrimitiveConversions;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class SkiaTextRenderer : IDisposable {
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
                command.Bounds.MinX + ((command.Bounds.Width - textWidth) / 2),
            TextHorizontalAlignment.End => command.Bounds.MaxX - textWidth,
            _ => command.Bounds.MinX,
        };
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent;
        var y = command.VerticalAlignment switch {
            TextVerticalAlignment.Top => command.Bounds.MinY - metrics.Ascent,
            TextVerticalAlignment.Bottom => command.Bounds.MaxY - metrics.Descent,
            _ => command.Bounds.MinY +
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

    private SKTypeface? GetLineTypeface(FontFace? font) {
        return font is null
            ? _fontCache.PrimaryTypeface
            : _fontCache.ResolveTypeface(font);
    }

}
