using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfo {
    public BitmapInfoHeader Header;
    public uint Colors;
}
