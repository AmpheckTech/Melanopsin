// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Text;

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal static class FontNameReader {
    public static (string FamilyName, string StyleName) Read(
        ReadOnlyMemory<byte> data,
        int tableOffset,
        int tableLength,
        string fallbackName) {
        if (tableLength < 6 ||
            tableLength > OpenTypeLimits.MaximumNameTableBytes ||
            tableOffset > data.Length - tableLength) {
            throw new InvalidDataException(
                "The font 'name' table is truncated.");
        }

        var reader = new FontBinaryReader(data.Span.Slice(tableOffset, tableLength));
        var count = reader.ReadUInt16(2);
        if (count > OpenTypeLimits.MaximumNameRecords) {
            throw new InvalidDataException(
                "The font 'name' table declares too many records.");
        }

        var stringsOffset = reader.ReadUInt16(4);
        var familyName = FindName(reader, stringsOffset, count, 1);
        var styleName = FindName(reader, stringsOffset, count, 2);
        return (
            string.IsNullOrWhiteSpace(familyName) ? fallbackName : familyName,
            string.IsNullOrWhiteSpace(styleName) ? "Regular" : styleName);
    }

    private static string? FindName(
        FontBinaryReader reader,
        int stringsOffset,
        int count,
        int nameId) {
        string? fallback = null;
        for (var index = 0; index < count; index++) {
            var recordOffset = 6 + (index * 12);
            var platformId = reader.ReadUInt16(recordOffset);
            var encodingId = reader.ReadUInt16(recordOffset + 2);
            var languageId = reader.ReadUInt16(recordOffset + 4);
            var recordNameId = reader.ReadUInt16(recordOffset + 6);
            if (recordNameId != nameId) {
                continue;
            }

            var length = reader.ReadUInt16(recordOffset + 8);
            var offset = checked(stringsOffset + reader.ReadUInt16(recordOffset + 10));
            var bytes = reader.Slice(offset, length);
            string? value = platformId switch {
                0 => Encoding.BigEndianUnicode.GetString(bytes),
                3 when encodingId is 1 or 10 => Encoding.BigEndianUnicode.GetString(bytes),
                1 => Encoding.ASCII.GetString(bytes),
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            if (platformId == 3 && languageId == 0x0409) {
                return value;
            }

            fallback ??= value;
        }

        return fallback;
    }
}
