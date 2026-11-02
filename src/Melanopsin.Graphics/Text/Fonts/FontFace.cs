using System.Text;
using Melanopsin.Graphics.Text.Fonts.OpenType;

namespace Melanopsin.Graphics.Text.Fonts;

public sealed class FontFace : ITextMeasurer {
    private readonly ReadOnlyMemory<byte> _data;
    private readonly CmapLookup _cmap;
    private readonly int _glyphCount;
    private readonly int _hmtxOffset;
    private readonly int _horizontalMetricCount;

    internal FontFace(
        byte[] data,
        string? sourceName,
        string familyName,
        string styleName,
        FontMetrics metrics,
        int glyphCount,
        int hmtxOffset,
        int horizontalMetricCount,
        CmapLookup cmap,
        int faceIndex,
        bool hasColorGlyphs) {
        _data = data.AsMemory();
        SourceName = sourceName;
        FamilyName = familyName;
        StyleName = styleName;
        Metrics = metrics;
        _glyphCount = glyphCount;
        _hmtxOffset = hmtxOffset;
        _horizontalMetricCount = horizontalMetricCount;
        _cmap = cmap;
        FaceIndex = faceIndex;
        HasColorGlyphs = hasColorGlyphs;
    }

    public string? SourceName { get; }

    public string FamilyName { get; }

    public string StyleName { get; }

    public FontMetrics Metrics { get; }

    public int FaceIndex { get; }

    /// <summary>
    /// True when the face stores colour glyphs through COLR/CPAL, CBDT/CBLC,
    /// sbix or SVG, so emoji sequences can be drawn in colour.
    /// </summary>
    public bool HasColorGlyphs { get; }

    public ReadOnlyMemory<byte> Data => _data;

    public static FontFace Load(string path, int faceIndex = 0) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length > OpenTypeLimits.MaximumFontBytes) {
            throw new InvalidDataException(
                $"Font data from '{path}' exceeds the supported size limit.");
        }

        var data = new byte[checked((int)stream.Length)];
        stream.ReadExactly(data);
        return Parse(data, path, faceIndex);
    }

    public static FontFace Parse(
        ReadOnlySpan<byte> data,
        string? sourceName = null,
        int faceIndex = 0) {
        if (data.Length > OpenTypeLimits.MaximumFontBytes) {
            throw new InvalidDataException(
                $"Font data from '{sourceName ?? "the provided buffer"}' " +
                "exceeds the supported size limit.");
        }

        try {
            return OpenTypeFontParser.Parse(data.ToArray(), sourceName, faceIndex);
        } catch (InvalidDataException exception) {
            throw CreateMalformedDataException(sourceName, exception);
        } catch (Exception exception) when (
            exception is OverflowException or
                IndexOutOfRangeException or
                EndOfStreamException) {
            throw CreateMalformedDataException(sourceName, exception);
        }
    }

    public Stream OpenStream() {
        return new MemoryStream(_data.ToArray(), writable: false);
    }

    public int GetGlyphId(Rune rune) {
        try {
            return _cmap.GetGlyphId(rune.Value);
        } catch (Exception exception) when (IsMalformedDataException(exception)) {
            throw CreateMalformedDataException(SourceName, exception);
        }
    }

    public int GetAdvanceWidth(int glyphId) {
        if (glyphId < 0 || glyphId >= _glyphCount) {
            return 0;
        }

        var metricIndex = Math.Min(
            glyphId,
            _horizontalMetricCount - 1);
        var offset = _hmtxOffset + (metricIndex * 4);
        try {
            var reader = new FontBinaryReader(_data.Span);
            return reader.ReadUInt16(offset);
        } catch (Exception exception) when (IsMalformedDataException(exception)) {
            throw CreateMalformedDataException(SourceName, exception);
        }
    }

    public TextMetrics MeasureText(string text, float fontSize) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        var scale = Metrics.Scale(fontSize);
        var width = 0f;
        foreach (var rune in text.EnumerateRunes()) {
            var glyphId = GetGlyphId(rune);
            if (glyphId <= 0 || glyphId >= _glyphCount) {
                width += fontSize;
                continue;
            }

            width += GetAdvanceWidth(glyphId) * scale;
        }

        return new TextMetrics(
            width,
            Metrics.LineHeight(fontSize),
            Metrics.Ascender * scale,
            Metrics.Descender * scale);
    }

    private static InvalidDataException CreateMalformedDataException(
        string? sourceName,
        Exception inner) {
        return new InvalidDataException(
            $"Font data from '{sourceName ?? "the provided buffer"}' is malformed.",
            inner);
    }

    private static bool IsMalformedDataException(Exception exception) {
        return exception is InvalidDataException or
            OverflowException or
            IndexOutOfRangeException or
            EndOfStreamException;
    }
}
