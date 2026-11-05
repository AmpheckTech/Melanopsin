using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader {
    public uint Size;
    public int Width;
    public int Height;
    public ushort Planes;
    public ushort BitCount;
    public uint Compression;
    public uint ImageSize;
    public int XPelsPerMeter;
    public int YPelsPerMeter;
    public uint ColorsUsed;
    public uint ColorsImportant;
}
