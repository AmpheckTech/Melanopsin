using System.Text;

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal static class FontNameReader {
    public static (string FamilyName, string StyleName) Read(
        ReadOnlyMemory<byte> data,
        int tableOffset,
        string fallbackName) {
        var reader = new FontBinaryReader(data.Span);
        var count = reader.ReadUInt16(tableOffset + 2);
        var stringsOffset = tableOffset + reader.ReadUInt16(tableOffset + 4);
        var familyName = FindName(reader, tableOffset, stringsOffset, count, 1);
        var styleName = FindName(reader, tableOffset, stringsOffset, count, 2);
        return (
            string.IsNullOrWhiteSpace(familyName) ? fallbackName : familyName,
            string.IsNullOrWhiteSpace(styleName) ? "Regular" : styleName);
    }

    private static string? FindName(
        FontBinaryReader reader,
        int tableOffset,
        int stringsOffset,
        int count,
        int nameId) {
        string? fallback = null;
        for (var index = 0; index < count; index++) {
            var recordOffset = tableOffset + 6 + (index * 12);
            var platformId = reader.ReadUInt16(recordOffset);
            var encodingId = reader.ReadUInt16(recordOffset + 2);
            var languageId = reader.ReadUInt16(recordOffset + 4);
            var recordNameId = reader.ReadUInt16(recordOffset + 6);
            if (recordNameId != nameId) {
                continue;
            }

            var length = reader.ReadUInt16(recordOffset + 8);
            var offset = stringsOffset + reader.ReadUInt16(recordOffset + 10);
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
