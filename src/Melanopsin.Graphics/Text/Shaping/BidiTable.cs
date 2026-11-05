// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;

namespace Melanopsin.Graphics.Text;

/// <summary>
/// The packed Unicode bidi class and bracket tables embedded by the graphics
/// assembly.
/// </summary>
internal static class BidiTable {
    public const int LastCodePoint = 0x10FFFF;

    private const string ResourceName = "Melanopsin.Graphics.Text.Shaping.BidiData.bin";
    private const int HeaderSize = 24;
    private const int ClassRowSize = 4;
    private const int BracketSize = 8;
    private const int StartMask = (1 << 21) - 1;
    private const int BracketMask = (1 << 21) - 1;
    private const byte FormatVersion = 1;
    private const int SourceVersion = 170000;

    private static readonly byte[] Data = Load();
    private static readonly int RowCount =
        BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(8));
    private static readonly int BracketCount =
        BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(16));
    private static readonly int BracketOffset = HeaderSize + (RowCount * ClassRowSize);

    public static int Find(int codePoint) {
        var low = 0;
        var high = RowCount - 1;
        var found = 0;
        while (low <= high) {
            var middle = (low + high) >> 1;
            if (StartOf(middle) <= codePoint) {
                found = middle;
                low = middle + 1;
            } else {
                high = middle - 1;
            }
        }

        return found;
    }

    public static BidiClass Class(int row) {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(
            Data.AsSpan(HeaderSize + (row * ClassRowSize)));
        return (BidiClass)(value >> 21);
    }

    public static bool TryGetBracket(
        int codePoint,
        out int pairedCodePoint,
        out bool isOpen) {
        var low = 0;
        var high = BracketCount - 1;
        while (low <= high) {
            var middle = (low + high) >> 1;
            var packed = ReadBracket(middle);
            var current = (int)(packed & BracketMask);
            if (codePoint < current) {
                high = middle - 1;
                continue;
            }

            if (codePoint > current) {
                low = middle + 1;
                continue;
            }

            pairedCodePoint = (int)((packed >> 21) & BracketMask);
            isOpen = ((packed >> 42) & 1) != 0;
            return true;
        }

        pairedCodePoint = 0;
        isOpen = false;
        return false;
    }

    private static int StartOf(int row) {
        var packed = BinaryPrimitives.ReadUInt32LittleEndian(
            Data.AsSpan(HeaderSize + (row * ClassRowSize)));
        return (int)(packed & StartMask);
    }

    private static ulong ReadBracket(int index) {
        return BinaryPrimitives.ReadUInt64LittleEndian(
            Data.AsSpan(BracketOffset + (index * BracketSize)));
    }

    private static byte[] Load() {
        var assembly = typeof(BidiTable).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"The embedded bidi table '{ResourceName}' is missing.");
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        Validate(data);
        return data;
    }

    private static void Validate(byte[] data) {
        if (data.Length < HeaderSize ||
            !data.AsSpan(0, 4).SequenceEqual("MBDX"u8)) {
            throw new InvalidOperationException(
                "The embedded bidi table is not a Melanopsin table.");
        }

        if (data[4] != FormatVersion) {
            throw new InvalidOperationException(
                $"The embedded bidi table has format version {data[4]}, " +
                $"but version {FormatVersion} is expected.");
        }

        var rowCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8));
        var version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12));
        var bracketCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16));
        if (rowCount <= 0 || bracketCount < 0 ||
            data.Length != HeaderSize + (rowCount * ClassRowSize) +
                (bracketCount * BracketSize)) {
            throw new InvalidOperationException("The embedded bidi table is truncated.");
        }

        if (version != SourceVersion) {
            throw new InvalidOperationException(
                $"The embedded bidi table was generated from Unicode {version}, " +
                $"but Unicode {SourceVersion} is expected.");
        }
    }
}
