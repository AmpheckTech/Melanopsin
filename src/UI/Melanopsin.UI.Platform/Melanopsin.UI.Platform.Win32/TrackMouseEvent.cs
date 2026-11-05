using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct TrackMouseEvent {
    public uint Size;
    public uint Flags;
    public IntPtr Window;
    public uint HoverTime;
}
