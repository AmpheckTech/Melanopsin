using System;
using System.Buffers.Binary;

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal readonly ref struct FontBinaryReader(ReadOnlySpan<byte> data) {
    private readonly ReadOnlySpan<byte> _data = data;

    public int Length => _data.Length;

    public ushort ReadUInt16(int offset) {
        Require(offset, sizeof(ushort));
        return BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(offset, sizeof(ushort)));
    }

    public short ReadInt16(int offset) {
        Require(offset, sizeof(short));
        return BinaryPrimitives.ReadInt16BigEndian(_data.Slice(offset, sizeof(short)));
    }

    public uint ReadUInt32(int offset) {
        Require(offset, sizeof(uint));
        return BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(offset, sizeof(uint)));
    }

    public ReadOnlySpan<byte> Slice(int offset, int length) {
        Require(offset, length);
        return _data.Slice(offset, length);
    }

    private void Require(int offset, int length) {
        if (offset < 0 || length < 0 || offset > _data.Length - length) {
            throw new InvalidDataException(
                $"OpenType data is truncated at offset {offset} while reading {length} bytes.");
        }
    }
}
