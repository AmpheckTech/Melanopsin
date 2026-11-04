using Melanopsin.Graphics.Text.Fonts;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

/// <summary>
/// A Skia-backed raster backend. Each render target owns an independent
/// BGRA8888 bitmap. A primary font is optional for scenes that contain no text.
/// </summary>
public sealed class SkiaRenderBackend : IRenderBackend {
    private readonly FontFace? _font;
    private readonly SKTypeface? _typeface;
    private readonly bool _ownsTypeface;
    private readonly SkiaRasterCacheOptions _rasterCacheOptions;
    private int _disposed;
    private Exception? _cleanupFailure;

    public SkiaRenderBackend(SkiaRasterCacheOptions rasterCacheOptions = default) {
        _rasterCacheOptions = rasterCacheOptions == default
            ? SkiaRasterCacheOptions.Default
            : rasterCacheOptions;
    }

    public SkiaRenderBackend(
        FontFace font,
        SkiaRasterCacheOptions rasterCacheOptions = default) {
        ArgumentNullException.ThrowIfNull(font);
        _rasterCacheOptions = rasterCacheOptions == default
            ? SkiaRasterCacheOptions.Default
            : rasterCacheOptions;
        using var stream = font.OpenStream();
        _font = font;
        _typeface = SKTypeface.FromStream(stream, font.FaceIndex) ??
            throw new InvalidOperationException(
                $"Skia could not create a typeface from font '{font.SourceName ?? font.FamilyName}'.");
        _ownsTypeface = true;
    }

    public string Name => "Skia";

    /// <summary>
    /// The failure observed during the most recent disposal. Disposal records
    /// this value instead of throwing so it cannot mask an active exception.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public IRenderTarget CreateTarget(RenderTargetDescriptor descriptor) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return new SkiaRenderTarget(
            descriptor,
            _font,
            _typeface,
            _rasterCacheOptions);
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        Exception? failure = null;
        if (_ownsTypeface && _typeface is not null) {
            SkiaCleanup.Capture(ref failure, _typeface.Dispose);
        }

        Volatile.Write(ref _cleanupFailure, failure);
    }
}
