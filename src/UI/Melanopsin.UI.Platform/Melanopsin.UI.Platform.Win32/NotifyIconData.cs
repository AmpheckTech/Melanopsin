// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NotifyIconData {
    internal uint Size;
    internal IntPtr Window;
    internal uint Id;
    internal uint Flags;
    internal uint CallbackMessage;
    internal IntPtr Icon;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    internal string Tip;

    internal uint State;
    internal uint StateMask;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    internal string Info;

    internal uint TimeoutOrVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    internal string InfoTitle;

    internal uint InfoFlags;
    internal Guid ItemGuid;
    internal IntPtr BalloonIcon;
}
