using System.Buffers;
using System.Text;
using Melanopsin.Graphics.Text;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaFontCache : IDisposable {
    private readonly FontFace? _primaryFont;
    private readonly SKFont? _glyphProbe;
    private readonly Dictionary<int, SKTypeface?> _fallbackTypefaces = [];
    private readonly Dictionary<FontFace, SKTypeface> _fontTypefaces = [];
    private readonly Dictionary<FontKey, SKFont> _fonts = [];
    private bool _disposed;

    public SkiaFontCache(FontFace? primaryFont, SKTypeface? typeface) {
        _primaryFont = primaryFont;
        PrimaryTypeface = typeface;
        _glyphProbe = typeface is null ? null : new SKFont(typeface);
    }

    public SKTypeface? PrimaryTypeface { get; }

    public List<(SKTypeface Typeface, int Start, int Length)> CreateTextRuns(
        string text,
        FontFace? preferredFont = null) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var runs = new List<(SKTypeface Typeface, int Start, int Length)>();
        if (text.Length == 0) {
            return runs;
        }

        if (PrimaryTypeface is null && preferredFont is null) {
            return runs;
        }

        var runStart = 0;
        SKTypeface? runTypeface = null;
        for (var index = 0; index < text.Length;) {
            var status = Rune.DecodeFromUtf16(
                text.AsSpan(index),
                out var rune,
                out var consumed);
            if (status != OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            var typeface = ResolveTypeface(rune, preferredFont);
            if (typeface is null) {
                if (runTypeface is not null) {
                    runs.Add((runTypeface, runStart, index - runStart));
                    runTypeface = null;
                }

                index += consumed;
                continue;
            }

            if (runTypeface is null) {
                runTypeface = typeface;
                runStart = index;
            } else if (!ReferenceEquals(runTypeface, typeface)) {
                runs.Add((runTypeface, runStart, index - runStart));
                runTypeface = typeface;
                runStart = index;
            }

            index += consumed;
        }

        if (runTypeface is not null) {
            runs.Add((runTypeface, runStart, text.Length - runStart));
        }

        return runs;
    }

    public SKTypeface ResolveTypeface(FontFace fontFace) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(fontFace, _primaryFont)) {
            return PrimaryTypeface ??
                throw new InvalidOperationException(
                    "The primary Skia typeface is unavailable.");
        }

        if (_fontTypefaces.TryGetValue(fontFace, out var cached)) {
            return cached;
        }

        using var stream = fontFace.OpenStream();
        var typeface = SKTypeface.FromStream(stream, fontFace.FaceIndex) ??
            throw new InvalidOperationException(
                $"Skia could not create a typeface for " +
                $"'{fontFace.SourceName ?? fontFace.FamilyName}'.");
        _fontTypefaces[fontFace] = typeface;
        return typeface;
    }

    public SKFont GetFont(SKTypeface typeface, float size) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = new FontKey(typeface, size);
        if (_fonts.TryGetValue(key, out var cached)) {
            return cached;
        }

        var font = new SKFont(typeface, size);
        _fonts[key] = font;
        return font;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        if (_glyphProbe is not null) {
            SkiaCleanup.Capture(ref failure, _glyphProbe.Dispose);
        }

        foreach (var font in _fonts.Values) {
            SkiaCleanup.Capture(ref failure, font.Dispose);
        }

        foreach (var fallback in _fallbackTypefaces.Values) {
            if (fallback is not null) {
                SkiaCleanup.Capture(ref failure, fallback.Dispose);
            }
        }

        foreach (var typeface in _fontTypefaces.Values) {
            if (!ReferenceEquals(typeface, PrimaryTypeface)) {
                SkiaCleanup.Capture(ref failure, typeface.Dispose);
            }
        }

        _fonts.Clear();
        _fallbackTypefaces.Clear();
        _fontTypefaces.Clear();
        SkiaCleanup.ThrowIfFailed(failure);
    }

    private SKTypeface? ResolveTypeface(
        Rune rune,
        FontFace? preferredFont) {
        if (preferredFont is not null &&
            preferredFont.GetGlyphId(rune) != 0) {
            return ResolveTypeface(preferredFont);
        }

        var primary = PrimaryTypeface;
        if (primary is not null &&
            _glyphProbe!.GetGlyph(rune.Value) != 0) {
            return primary;
        }

        if (_fallbackTypefaces.TryGetValue(rune.Value, out var cached)) {
            return cached ??
                (preferredFont is null
                    ? primary
                    : ResolveTypeface(preferredFont));
        }

        var fallback = SKFontManager.Default.MatchCharacter(rune.Value);
        _fallbackTypefaces[rune.Value] = fallback;
        if (fallback is not null) {
            return fallback;
        }

        return preferredFont is null
            ? primary
            : ResolveTypeface(preferredFont);
    }
}
