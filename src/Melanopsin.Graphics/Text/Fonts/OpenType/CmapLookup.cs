// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal sealed class CmapLookup {
    private readonly int _format;
    private readonly ReadOnlyMemory<byte> _data;

    private CmapLookup(ReadOnlyMemory<byte> data, int format) {
        _data = data;
        _format = format;
    }

    public static CmapLookup Create(
        ReadOnlyMemory<byte> fontData,
        int cmapOffset,
        int cmapLength) {
        if (cmapLength < 4) {
            throw new InvalidDataException("The cmap table is truncated.");
        }

        var table = fontData.Slice(cmapOffset, cmapLength);
        var reader = new FontBinaryReader(table.Span);
        var recordCount = reader.ReadUInt16(2);
        if (recordCount > OpenTypeLimits.MaximumCmapRecords) {
            throw new InvalidDataException(
                "The cmap table declares too many encoding records.");
        }

        var recordsEnd = checked(4 + (recordCount * 8));
        if (recordsEnd > cmapLength) {
            throw new InvalidDataException(
                "The cmap encoding record array extends past the cmap table.");
        }

        int? selectedOffset = null;
        var selectedFormat = 0;
        var bestRank = -1;
        for (var index = 0; index < recordCount; index++) {
            var recordOffset = checked(4 + (index * 8));
            var platformId = reader.ReadUInt16(recordOffset);
            var encodingId = reader.ReadUInt16(recordOffset + 2);
            var relativeOffset = reader.ReadUInt32(recordOffset + 4);
            if (relativeOffset > (uint)cmapLength - 4) {
                throw new InvalidDataException(
                    "A cmap encoding record points outside the cmap table.");
            }

            var subtableOffset = checked((int)relativeOffset);
            var format = reader.ReadUInt16(subtableOffset);
            var rank = GetRank(platformId, encodingId, format);
            if (rank > bestRank) {
                selectedOffset = subtableOffset;
                selectedFormat = format;
                bestRank = rank;
            }
        }

        if (selectedOffset is not { } offset) {
            throw new InvalidDataException(
                "The font does not contain a supported Unicode cmap table.");
        }

        var length = GetSubtableLength(
            reader,
            offset,
            cmapLength,
            selectedFormat);
        var subtable = table.Slice(offset, length);
        if (selectedFormat == 4) {
            ValidateFormat4(subtable);
        } else {
            ValidateFormat12(subtable);
        }

        return new CmapLookup(subtable, selectedFormat);
    }

    public int GetGlyphId(int codePoint) {
        return _format switch {
            4 => GetFormat4GlyphId(codePoint),
            12 => GetFormat12GlyphId(codePoint),
            _ => 0,
        };
    }

    private int GetFormat4GlyphId(int codePoint) {
        if (codePoint is < 0 or > 0xFFFF) {
            return 0;
        }

        var reader = new FontBinaryReader(_data.Span);
        var segmentCount = reader.ReadUInt16(6) / 2;
        var endCodesOffset = 14;
        var startCodesOffset = endCodesOffset + (segmentCount * 2) + 2;
        var deltasOffset = startCodesOffset + (segmentCount * 2);
        var rangeOffsetsOffset = deltasOffset + (segmentCount * 2);

        for (var index = 0; index < segmentCount; index++) {
            var endCode = reader.ReadUInt16(endCodesOffset + (index * 2));
            if (codePoint > endCode) {
                continue;
            }

            var startCode = reader.ReadUInt16(startCodesOffset + (index * 2));
            if (codePoint < startCode) {
                return 0;
            }

            var delta = reader.ReadInt16(deltasOffset + (index * 2));
            var rangeOffsetPosition = rangeOffsetsOffset + (index * 2);
            var rangeOffset = reader.ReadUInt16(rangeOffsetPosition);
            if (rangeOffset == 0) {
                return (codePoint + delta) & 0xFFFF;
            }

            var glyphOffset = rangeOffsetPosition +
                rangeOffset +
                ((codePoint - startCode) * 2);
            var glyphId = reader.ReadUInt16(glyphOffset);
            return glyphId == 0
                ? 0
                : (glyphId + delta) & 0xFFFF;
        }

        return 0;
    }

    private int GetFormat12GlyphId(int codePoint) {
        var reader = new FontBinaryReader(_data.Span);
        var groupCount = checked((int)reader.ReadUInt32(12));
        var groupOffset = 16;
        var low = 0;
        var high = groupCount - 1;

        while (low <= high) {
            var middle = low + ((high - low) / 2);
            var recordOffset = checked(groupOffset + (middle * 12));
            var start = reader.ReadUInt32(recordOffset);
            var end = reader.ReadUInt32(recordOffset + 4);
            if (codePoint < start) {
                high = middle - 1;
            } else if (codePoint > end) {
                low = middle + 1;
            } else {
                var startGlyph = reader.ReadUInt32(recordOffset + 8);
                var glyphId = startGlyph + (uint)(codePoint - start);
                return glyphId <= int.MaxValue ? (int)glyphId : 0;
            }
        }

        return 0;
    }

    private static int GetSubtableLength(
        FontBinaryReader reader,
        int offset,
        int tableLength,
        int format) {
        uint declaredLength = format switch {
            4 => reader.ReadUInt16(offset + 2),
            12 => reader.ReadUInt32(offset + 4),
            _ => throw new InvalidDataException(
                $"Unsupported cmap format {format}."),
        };
        if (declaredLength < 4 ||
            declaredLength > (uint)tableLength - (uint)offset) {
            throw new InvalidDataException(
                $"The cmap format {format} subtable has an invalid length.");
        }

        return checked((int)declaredLength);
    }

    private static void ValidateFormat4(ReadOnlyMemory<byte> subtable) {
        var reader = new FontBinaryReader(subtable.Span);
        var segmentCountX2 = reader.ReadUInt16(6);
        if (segmentCountX2 == 0 || (segmentCountX2 & 1) != 0) {
            throw new InvalidDataException(
                "The cmap format 4 subtable declares an invalid segment count.");
        }

        var segmentCount = segmentCountX2 / 2;
        var requiredLength = checked(16 + (segmentCount * 8));
        if (requiredLength > subtable.Length) {
            throw new InvalidDataException(
                "The cmap format 4 segment arrays extend past the subtable.");
        }

        var endCodesOffset = 14;
        var startCodesOffset = checked(endCodesOffset + (segmentCount * 2) + 2);
        var deltasOffset = checked(startCodesOffset + (segmentCount * 2));
        var rangeOffsetsOffset = checked(deltasOffset + (segmentCount * 2));
        for (var index = 0; index < segmentCount; index++) {
            var startCode = reader.ReadUInt16(startCodesOffset + (index * 2));
            var endCode = reader.ReadUInt16(endCodesOffset + (index * 2));
            if (startCode > endCode) {
                throw new InvalidDataException(
                    "The cmap format 4 subtable contains an inverted segment.");
            }

            var rangeOffsetPosition = rangeOffsetsOffset + (index * 2);
            var rangeOffset = reader.ReadUInt16(rangeOffsetPosition);
            if (rangeOffset == 0) {
                continue;
            }

            var lastGlyphOffset = checked(
                rangeOffsetPosition +
                rangeOffset +
                ((endCode - startCode) * 2) +
                sizeof(ushort));
            if (lastGlyphOffset > subtable.Length) {
                throw new InvalidDataException(
                    "A cmap format 4 glyph index array extends past the subtable.");
            }
        }
    }

    private static void ValidateFormat12(ReadOnlyMemory<byte> subtable) {
        var reader = new FontBinaryReader(subtable.Span);
        var groupCount = reader.ReadUInt32(12);
        if (groupCount == 0) {
            throw new InvalidDataException(
                "The cmap format 12 subtable declares no groups.");
        }

        var requiredLength = 16UL + (groupCount * 12UL);
        if (requiredLength > (ulong)subtable.Length) {
            throw new InvalidDataException(
                "The cmap format 12 groups extend past the subtable.");
        }

        uint? previousEnd = null;
        for (var index = 0; index < groupCount; index++) {
            var recordOffset = checked(16 + ((int)index * 12));
            var start = reader.ReadUInt32(recordOffset);
            var end = reader.ReadUInt32(recordOffset + 4);
            var startGlyph = reader.ReadUInt32(recordOffset + 8);
            if (start > end) {
                throw new InvalidDataException(
                    "The cmap format 12 subtable contains an inverted group.");
            }

            if (previousEnd is { } previous && start <= previous) {
                throw new InvalidDataException(
                    "The cmap format 12 groups are not strictly ordered.");
            }

            var lastGlyph = startGlyph + (end - start);
            if (lastGlyph < startGlyph || lastGlyph > int.MaxValue) {
                throw new InvalidDataException(
                    "A cmap format 12 group maps beyond the supported glyph range.");
            }

            previousEnd = end;
        }
    }

    private static int GetRank(int platformId, int encodingId, int format) {
        if (format == 12 &&
            (platformId == 0 || (platformId == 3 && encodingId == 10))) {
            return 4;
        }

        if (format == 4 &&
            (platformId == 0 || platformId == 3)) {
            return 3;
        }

        return -1;
    }
}
