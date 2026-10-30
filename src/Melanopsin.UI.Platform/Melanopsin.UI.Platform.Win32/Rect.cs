using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct Rect {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
