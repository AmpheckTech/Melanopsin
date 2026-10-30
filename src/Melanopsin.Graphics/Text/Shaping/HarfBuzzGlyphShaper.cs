using System.Text;
using HarfBuzzSharp;
using Melanopsin.Graphics.Text.Fonts;
using Unicode.Bidi;

namespace Melanopsin.Graphics.Text;

internal sealed class HarfBuzzGlyphShaper : IDisposable {
    private readonly FontCollection _fonts;
    private readonly Dictionary<FontFace, HarfBuzzFont> _nativeFonts = [];
    private readonly Lock _shapeLock = new();
    private bool _disposed;

    public HarfBuzzGlyphShaper(FontCollection fonts) {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
    }

    public List<TextRun> ShapeToken(
        string text,
        int start,
        int length,
        float fontSize,
        TextDirection baseDirection,
        BidiInfo bidi,
        FontFace? preferredFont) {
        lock (_shapeLock) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ShapeTokenCore(
                text,
                start,
                length,
                fontSize,
                baseDirection,
                bidi,
                preferredFont);
        }
    }

    private List<TextRun> ShapeTokenCore(
        string text,
        int start,
        int length,
        float fontSize,
        TextDirection baseDirection,
        BidiInfo bidi,
        FontFace? preferredFont) {
        var runs = new List<TextRun>();
        var segmentStart = start;
        var segmentEnd = start;
        FontFace? segmentFont = null;
        var segmentDirection = baseDirection;

        for (var index = start; index < start + length;) {
            var status = Rune.DecodeFromUtf16(
                text.AsSpan(index, start + length - index),
                out var rune,
                out var consumed);
            if (status != System.Buffers.OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            var font = IsDefaultIgnorable(rune) && segmentFont is not null
                ? segmentFont
                : ResolveFont(rune, preferredFont);
            var direction = IsDefaultIgnorable(rune) && segmentFont is not null
                ? segmentDirection
                : TextDirectionResolver.ResolveRune(
                    rune,
                    index,
                    baseDirection,
                    bidi);
            if (segmentFont is null) {
                segmentFont = font;
                segmentDirection = direction;
                segmentStart = index;
            } else if (!ReferenceEquals(segmentFont, font) ||
                       segmentDirection != direction) {
                runs.Add(ShapeSegment(
                    text,
                    segmentStart,
                    segmentEnd - segmentStart,
                    segmentFont,
                    segmentDirection,
                    fontSize));
                segmentFont = font;
                segmentDirection = direction;
                segmentStart = index;
            }

            index += consumed;
            segmentEnd = index;
        }

        if (segmentFont is not null && segmentEnd > segmentStart) {
            runs.Add(ShapeSegment(
                text,
                segmentStart,
                segmentEnd - segmentStart,
                segmentFont,
                segmentDirection,
                fontSize));
        }

        return runs;
    }

    public LineMetrics GetLineMetrics(
        FontFace font,
        float fontSize) {
        lock (_shapeLock) {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        var scale = fontSize / font.Metrics.UnitsPerEm;
        return new LineMetrics(
            font.Metrics.Ascender * scale,
            Math.Abs(font.Metrics.Descender) * scale,
            font.Metrics.LineHeight(fontSize));
    }

    public void Dispose() {
        List<Exception>? failures = null;
        lock (_shapeLock) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            foreach (var font in _nativeFonts.Values) {
                try {
                    font.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }

            _nativeFonts.Clear();
        }

        if (failures is not null) {
            throw new AggregateException(
                "One or more HarfBuzz fonts failed to dispose cleanly.",
                failures);
        }
    }

    private TextRun ShapeSegment(
        string text,
        int start,
        int length,
        FontFace fontFace,
        TextDirection direction,
        float fontSize) {
        TextGlyph[] glyphs;
        float width;
        var scale = fontSize / fontFace.Metrics.UnitsPerEm;
        lock (_shapeLock) {
            var nativeFont = GetNativeFont(fontFace);
            using var buffer = new HarfBuzzSharp.Buffer();
            buffer.AddUtf16(text.Substring(start, length));
            buffer.GuessSegmentProperties();
            buffer.Direction = direction == TextDirection.RightToLeft
                ? HarfBuzzSharp.Direction.RightToLeft
                : HarfBuzzSharp.Direction.LeftToRight;
            nativeFont.Font.Shape(buffer);

            var infos = buffer.GlyphInfos;
            var positions = buffer.GlyphPositions;
            glyphs = new TextGlyph[infos.Length];
            width = 0f;
            for (var index = 0; index < glyphs.Length; index++) {
                var info = infos[index];
                var position = positions[index];
                var xAdvance = position.XAdvance * scale;
                glyphs[index] = new TextGlyph(
                    info.Codepoint,
                    checked((int)info.Cluster) + start,
                    xAdvance,
                    position.YAdvance * scale,
                    position.XOffset * scale,
                    position.YOffset * scale);
                width += xAdvance;
            }
        }

        return new TextRun(
            fontFace,
            direction,
            fontSize,
            x: 0,
            start,
            length,
            width,
            fontFace.Metrics.Ascender * scale,
            Math.Abs(fontFace.Metrics.Descender) * scale,
            glyphs);
    }

    private FontFace ResolveFont(Rune rune, FontFace? preferredFont) {
        return preferredFont is not null && preferredFont.GetGlyphId(rune) != 0
            ? preferredFont
            : _fonts.Resolve(rune);
    }

    private HarfBuzzFont GetNativeFont(FontFace fontFace) {
        if (_nativeFonts.TryGetValue(fontFace, out var cached)) {
            return cached;
        }

        var native = new HarfBuzzFont(fontFace);
        _nativeFonts[fontFace] = native;
        return native;
    }

    private static bool IsDefaultIgnorable(Rune rune) {
        return rune.Value is
            0x00AD or 0x034F or 0x061C or 0x115F or 0x1160 or
            0x17B4 or 0x17B5 or 0x180B or 0x180C or 0x180D or
            0x180E or 0x180F or 0x200B or 0x200C or 0x200D or
            0x200E or 0x200F or 0x202A or 0x202B or 0x202C or
            0x202D or 0x202E or 0x2060 or 0x2061 or 0x2062 or
            0x2063 or 0x2064 or 0xFE00 or 0xFE0F or 0xFEFF or
            >= 0xE0100 and <= 0xE01EF;
    }
}
