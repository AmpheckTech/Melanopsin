using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct Message {
    public IntPtr Window;
    public uint Value;
    public IntPtr WParam;
    public IntPtr LParam;
    public uint Time;
    public int X;
    public int Y;
}
