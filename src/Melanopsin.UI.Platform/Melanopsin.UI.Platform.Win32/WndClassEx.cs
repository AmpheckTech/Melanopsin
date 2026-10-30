using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WndClassEx {
    public uint Size;
    public uint Style;
    public IntPtr WindowProcedure;
    public int ClassExtra;
    public int WindowExtra;
    public IntPtr Instance;
    public IntPtr Icon;
    public IntPtr Cursor;
    public IntPtr Background;
    public string? MenuName;
    public string ClassName;
    public IntPtr SmallIcon;
}
