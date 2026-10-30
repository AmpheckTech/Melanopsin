using Melanopsin.Graphics.Text;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaCommandRenderer : IDisposable {
    private readonly SkiaFontCache _fontCache;
    private readonly SkiaImageResourceCache _imageCache = new();
    private readonly SkiaTextRenderer _textRenderer;
    private readonly SKPaint _fillPaint = new();
    private readonly SKPaint _strokePaint = new();
    private bool _disposed;

    public SkiaCommandRenderer(FontFace? primaryFont, SKTypeface? typeface) {
        _fontCache = new SkiaFontCache(primaryFont, typeface);
        _textRenderer = new SkiaTextRenderer(_fontCache);
    }

    public void Draw(SKCanvas canvas, RenderCommand command) {
        switch (command) {
            case FillRectangleCommand fill:
                DrawFill(canvas, fill);
                break;
            case StrokeRectangleCommand stroke:
                DrawStroke(canvas, stroke);
                break;
            case DrawTextCommand text:
                _textRenderer.DrawText(canvas, text);
                break;
            case DrawEditableTextCommand editableText:
                _textRenderer.DrawEditableText(canvas, editableText);
                break;
            case DrawGlyphRunCommand glyphRun:
                _textRenderer.DrawGlyphRun(canvas, glyphRun);
                break;
            case DrawShapedEditableTextCommand shapedEditableText:
                _textRenderer.DrawShapedEditableText(canvas, shapedEditableText);
                break;
            case DrawImageCommand image:
                DrawImage(canvas, image);
                break;
            case DrawImageResourceCommand imageResource:
                DrawImageResource(canvas, imageResource);
                break;
            default:
                throw new NotSupportedException(
                    $"Skia does not support command '{command.GetType().Name}'.");
        }
    }

    public static bool TryGetCommandBounds(
        RenderCommand command,
        out Rect bounds) {
        switch (command) {
            case FillRectangleCommand fill:
                bounds = fill.Bounds.Inflate(1);
                return true;
            case StrokeRectangleCommand stroke:
                bounds = stroke.Bounds.Inflate(stroke.StrokeWidth + 1);
                return true;
            case DrawTextCommand text:
                bounds = text.Bounds.Inflate(1);
                return true;
            case DrawEditableTextCommand editableText:
                bounds = editableText.Bounds.Inflate(1);
                return true;
            case DrawGlyphRunCommand glyphRun:
                bounds = glyphRun.Bounds.Inflate(1);
                return true;
            case DrawShapedEditableTextCommand shapedEditableText:
                bounds = shapedEditableText.Bounds.Inflate(1);
                return true;
            case DrawImageCommand image:
                bounds = image.Bounds.Inflate(1);
                return true;
            case DrawImageResourceCommand imageResource:
                bounds = imageResource.Bounds.Inflate(1);
                return true;
            default:
                throw new NotSupportedException(
                    $"Skia cannot determine bounds for command '{command.GetType().Name}'.");
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        SkiaCleanup.Capture(ref failure, _fillPaint.Dispose);
        SkiaCleanup.Capture(ref failure, _strokePaint.Dispose);
        SkiaCleanup.Capture(ref failure, _textRenderer.Dispose);
        SkiaCleanup.Capture(ref failure, _fontCache.Dispose);
        SkiaCleanup.Capture(ref failure, _imageCache.Dispose);
        SkiaCleanup.ThrowIfFailed(failure);
    }

    private void DrawFill(SKCanvas canvas, FillRectangleCommand command) {
        _fillPaint.Color = ToSkColor(command.Color);
        _fillPaint.IsAntialias = true;
        _fillPaint.Style = SKPaintStyle.Fill;
        DrawShape(canvas, command.Bounds, command.CornerRadius, _fillPaint);
    }

    private void DrawStroke(SKCanvas canvas, StrokeRectangleCommand command) {
        _strokePaint.Color = ToSkColor(command.Color);
        _strokePaint.IsAntialias = true;
        _strokePaint.Style = SKPaintStyle.Stroke;
        _strokePaint.StrokeWidth = command.StrokeWidth;
        DrawShape(canvas, command.Bounds, command.CornerRadius, _strokePaint);
    }

    private static void DrawImage(SKCanvas canvas, DrawImageCommand command) {
        var colorType = command.PixelFormat switch {
            RenderPixelFormat.Bgra8888Premultiplied => SKColorType.Bgra8888,
            RenderPixelFormat.Rgba8888Premultiplied => SKColorType.Rgba8888,
            _ => throw new NotSupportedException(
                $"Skia does not support image format '{command.PixelFormat}'."),
        };
        var info = new SKImageInfo(
            command.Size.Width,
            command.Size.Height,
            colorType,
            SKAlphaType.Premul);
        using var image = SKImage.FromPixelCopy(
            info,
            command.Pixels.Span,
            command.Stride);
        DrawDecodedImage(canvas, image, command.Bounds, command.CornerRadius);
    }

    private void DrawImageResource(
        SKCanvas canvas,
        DrawImageResourceCommand command) {
        var image = _imageCache.Get(command.Image);
        DrawDecodedImage(canvas, image, command.Bounds, command.CornerRadius);
    }

    private static void DrawDecodedImage(
        SKCanvas canvas,
        SKImage image,
        Rect bounds,
        float cornerRadius) {
        if (cornerRadius > 0) {
            canvas.Save();
            canvas.ClipRoundRect(
                new SKRoundRect(
                    ToSkRect(bounds),
                    cornerRadius,
                    cornerRadius),
                antialias: true);
        }

        canvas.DrawImage(
            image,
            ToSkRect(bounds),
            new SKSamplingOptions(SKFilterMode.Linear));
        if (cornerRadius > 0) {
            canvas.Restore();
        }
    }

    private static void DrawShape(
        SKCanvas canvas,
        Rect bounds,
        float cornerRadius,
        SKPaint paint) {
        if (cornerRadius > 0) {
            canvas.DrawRoundRect(
                ToSkRect(bounds),
                cornerRadius,
                cornerRadius,
                paint);
        } else {
            canvas.DrawRect(ToSkRect(bounds), paint);
        }
    }

    private static SKRect ToSkRect(Rect rect) {
        return new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    private static SKColor ToSkColor(Color color) {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
