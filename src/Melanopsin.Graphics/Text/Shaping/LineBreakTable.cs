// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;

namespace Melanopsin.Graphics.Text;

/// <summary>
/// The packed Unicode <c>Line_Break</c> table that the assembly carries as the
/// embedded resource <c>Text/Shaping/LineBreakData.bin</c>.
/// </summary>
/// <remarks>
/// A row is four bytes - 21 bits of range start, 6 bits of
/// <see cref="LineBreakClass"/>, 5 bits of <see cref="LineBreakFlags"/> - sorted
/// so the last row starting at or before a code point contains it. A 16-byte
/// header holds the <c>MLBX</c> magic, format version, row count, and source
/// version. The table is embedded as data rather than compiled literals.
/// </remarks>
internal static class LineBreakTable {
    /// <summary>The highest code point the table covers.</summary>
    public const int LastCodePoint = 0x10FFFF;

    private const string ResourceName = "Melanopsin.Graphics.Text.Shaping.LineBreakData.bin";
    private const int HeaderSize = 16;
    private const int RowSize = 4;
    private const int ClassShift = 21;
    private const int FlagsShift = 27;
    private const int StartMask = (1 << ClassShift) - 1;
    private const int ClassMask = (1 << (FlagsShift - ClassShift)) - 1;
    private const int FlagsMask = (1 << (32 - FlagsShift)) - 1;
    private const byte FormatVersion = 1;
    private const int SourceVersion = 170000;

    private static readonly byte[] Rows = Load();
    private static readonly int RowCount = BinaryPrimitives.ReadInt32LittleEndian(Rows.AsSpan(8));

    /// <summary>
    /// Returns the index of the row that contains <paramref name="codePoint"/>.
    /// The code space is fully covered, so the search always succeeds for a
    /// code point the table is responsible for.
    /// </summary>
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

    /// <summary>Returns the line break class of a row.</summary>
    public static LineBreakClass Class(int row) =>
        (LineBreakClass)Field(row, ClassShift, ClassMask);

    /// <summary>Returns the extra properties of a row.</summary>
    public static LineBreakFlags Flags(int row) =>
        (LineBreakFlags)Field(row, FlagsShift, FlagsMask);

    private static int StartOf(int row) => (int)(Read(row) & StartMask);

    private static uint Field(int row, int shift, int mask) => (Read(row) >> shift) & (uint)mask;

    private static uint Read(int row) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Rows.AsSpan(HeaderSize + (row * RowSize)));

    private static byte[] Load() {
        var assembly = typeof(LineBreakTable).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"The embedded line break table '{ResourceName}' is missing.");
        var rows = new byte[stream.Length];
        stream.ReadExactly(rows);
        Validate(rows);
        return rows;
    }

    private static void Validate(byte[] rows) {
        if (rows.Length < HeaderSize || !rows.AsSpan(0, 4).SequenceEqual("MLBX"u8)) {
            throw new InvalidOperationException(
                "The embedded line break table is not a Melanopsin table.");
        }

        if (rows[4] != FormatVersion) {
            throw new InvalidOperationException(
                $"The embedded line break table has format version {rows[4]}, " +
                $"but version {FormatVersion} is expected.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(rows.AsSpan(8));
        if (count <= 0 || rows.Length != HeaderSize + (count * RowSize)) {
            throw new InvalidOperationException("The embedded line break table is truncated.");
        }

        var version = BinaryPrimitives.ReadInt32LittleEndian(rows.AsSpan(12));
        if (version != SourceVersion) {
            throw new InvalidOperationException(
                $"The embedded line break table was generated from source version " +
                $"{version}, but {SourceVersion} is expected.");
        }
    }
}
