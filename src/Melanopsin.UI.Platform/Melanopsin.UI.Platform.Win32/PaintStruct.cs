using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct PaintStruct {
    public IntPtr DeviceContext;
    public bool Erase;
    public Rect PaintRectangle;
    public bool Restore;
    public bool IncUpdate;
    public unsafe fixed byte Reserved[32];
}
