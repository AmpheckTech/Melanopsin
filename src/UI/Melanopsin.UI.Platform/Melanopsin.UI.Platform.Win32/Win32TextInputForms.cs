using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct Win32CompositionForm {
    public uint Style;
    public Point CurrentPosition;
    public Rect Area;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32CandidateForm {
    public uint Index;
    public uint Style;
    public Point CurrentPosition;
    public Rect Area;
}
