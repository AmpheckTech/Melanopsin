using System.Text;

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal static class OpenTypeFontParser {
    private const uint TrueTypeVersion = 0x00010000;
    private const uint OpenTypeCffVersion = 0x4F54544F;
    private const uint TrueTypeAppleVersion = 0x74727565;
    private const uint Type1Version = 0x74797031;
    private const uint CollectionVersion = 0x74746366;

    public static FontFace Parse(
        byte[] data,
        string? sourceName,
        int faceIndex) {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);

        var memory = data.AsMemory();
        var reader = new FontBinaryReader(memory.Span);
        if (reader.Length < 12) {
            throw new InvalidDataException("The font file is too small to contain an SFNT header.");
        }

        var headerOffset = 0;
        var version = reader.ReadUInt32(headerOffset);
        if (version == CollectionVersion) {
            var collectionFaceCount = reader.ReadUInt32(8);
            if (collectionFaceCount > OpenTypeLimits.MaximumCollectionFaces) {
                throw new InvalidDataException(
                    "The font collection declares too many faces.");
            }

            var faceCount = (int)collectionFaceCount;
            if (faceIndex >= faceCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(faceIndex),
                    $"Font collection contains {faceCount} face(s).");
            }

            headerOffset = checked((int)reader.ReadUInt32(
                checked(12 + (faceIndex * 4))));
            version = reader.ReadUInt32(headerOffset);
        } else if (faceIndex != 0) {
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex),
                "A single-face font only accepts face index zero.");
        }

        if (version is not TrueTypeVersion and
            not OpenTypeCffVersion and
            not TrueTypeAppleVersion and
            not Type1Version) {
            throw new InvalidDataException(
                "Only TrueType/OpenType fonts and TrueType Collections are supported. " +
                "WOFF files require a separate container parser.");
        }

        var tableCount = reader.ReadUInt16(headerOffset + 4);
        if (tableCount > OpenTypeLimits.MaximumTablesPerFace) {
            throw new InvalidDataException(
                "The font declares too many SFNT tables.");
        }

        var directoryLength = checked(12 + (tableCount * 16));
        _ = reader.Slice(headerOffset, directoryLength);
        var tables = new Dictionary<string, SfntTable>(tableCount, StringComparer.Ordinal);
        for (var index = 0; index < tableCount; index++) {
            var recordOffset = checked(headerOffset + 12 + (index * 16));
            var tag = Encoding.ASCII.GetString(reader.Slice(recordOffset, 4));
            var offset = checked((int)reader.ReadUInt32(recordOffset + 8));
            var length = checked((int)reader.ReadUInt32(recordOffset + 12));
            _ = reader.Slice(offset, length);
            if (!tables.TryAdd(tag, new SfntTable(tag, offset, length))) {
                throw new InvalidDataException(
                    $"The font contains duplicate '{tag}' tables.");
            }
        }

        // Colour faces carry one of these tables; the name differs per
        // technology (COLR v0/v1, embedded bitmaps, Apple sbix, SVG glyphs).
        var hasColorGlyphs =
            tables.ContainsKey("COLR") ||
            tables.ContainsKey("CBDT") ||
            tables.ContainsKey("sbix") ||
            tables.ContainsKey("SVG ");

        var head = RequireTable(tables, "head");
        RequireTableLength(head, 54);
        if (reader.ReadUInt32(head.Offset + 12) != 0x5F0F3CF5) {
            throw new InvalidDataException("The font head table has an invalid magic number.");
        }

        var unitsPerEm = reader.ReadUInt16(head.Offset + 18);
        if (unitsPerEm == 0) {
            throw new InvalidDataException("The font declares zero units per em.");
        }

        var hhea = RequireTable(tables, "hhea");
        RequireTableLength(hhea, 36);
        var ascender = reader.ReadInt16(hhea.Offset + 4);
        var descender = reader.ReadInt16(hhea.Offset + 6);
        var lineGap = reader.ReadInt16(hhea.Offset + 8);
        var horizontalMetricCount = reader.ReadUInt16(hhea.Offset + 34);
        if (horizontalMetricCount == 0) {
            throw new InvalidDataException("The font declares no horizontal metrics.");
        }

        var maxp = RequireTable(tables, "maxp");
        RequireTableLength(maxp, 6);
        var glyphCount = reader.ReadUInt16(maxp.Offset + 4);
        if (glyphCount == 0) {
            throw new InvalidDataException("The font declares no glyphs.");
        }

        var hmtx = RequireTable(tables, "hmtx");
        if (horizontalMetricCount > glyphCount) {
            throw new InvalidDataException(
                "The font declares more horizontal metrics than glyphs.");
        }

        var requiredHmtxLength =
            ((long)horizontalMetricCount * 4) +
            ((glyphCount - horizontalMetricCount) * 2L);
        if (hmtx.Length < requiredHmtxLength) {
            throw new InvalidDataException(
                "The hmtx table does not contain all declared glyph metrics.");
        }

        var metrics = new FontMetrics(unitsPerEm, ascender, descender, lineGap);
        var cmapTable = RequireTable(tables, "cmap");
        var cmap = CmapLookup.Create(
            memory,
            cmapTable.Offset,
            cmapTable.Length);
        var fallbackName = GetFallbackName(sourceName);
        var (familyName, styleName) = tables.TryGetValue("name", out var name)
            ? FontNameReader.Read(memory, name.Offset, name.Length, fallbackName)
            : (fallbackName, "Regular");

        return new FontFace(
            data,
            sourceName,
            familyName,
            styleName,
            metrics,
            glyphCount,
            hmtx.Offset,
            horizontalMetricCount,
            cmap,
            faceIndex,
            hasColorGlyphs);
    }

    private static SfntTable RequireTable(
        IReadOnlyDictionary<string, SfntTable> tables,
        string tag) {
        return tables.TryGetValue(tag, out var table)
            ? table
            : throw new InvalidDataException($"The font is missing the required '{tag}' table.");
    }

    private static void RequireTableLength(SfntTable table, int minimumLength) {
        if (table.Length < minimumLength) {
            throw new InvalidDataException(
                $"The font '{table.Tag}' table is truncated.");
        }
    }

    private static string GetFallbackName(string? sourceName) {
        return string.IsNullOrWhiteSpace(sourceName)
            ? "Unnamed Font"
            : Path.GetFileNameWithoutExtension(sourceName);
    }
}
