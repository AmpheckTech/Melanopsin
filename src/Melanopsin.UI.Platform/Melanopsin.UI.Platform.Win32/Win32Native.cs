using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

internal static class Win32Native {
    internal const uint DibRgbColors = 0;
    internal const uint Srccopy = 0x00CC0020;
    internal const int SwShow = 5;
    internal const uint PmRemove = 0x0001;
    internal const int CwUseDefault = unchecked((int)0x80000000);
    internal const uint WsOverlappedWindow = 0x00CF0000;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SpiGetWorkArea = 0x0030;
    internal const uint TmeLeave = 0x00000002;
    internal const int IdcArrow = 32512;
    internal const int VkTab = 0x09;
    internal const int VkReturn = 0x0D;
    internal const int VkShift = 0x10;
    internal const int VkControl = 0x11;
    internal const int VkA = 0x41;
    internal const int VkEscape = 0x1B;
    internal const int VkSpace = 0x20;
    internal const int VkBack = 0x08;
    internal const int VkHome = 0x24;
    internal const int VkEnd = 0x23;
    internal const int VkLeft = 0x25;
    internal const int VkRight = 0x27;
    internal const int VkDelete = 0x2E;
    internal const uint WmDestroy = 0x0002;
    internal const uint WmClose = 0x0010;
    internal const uint WmPaint = 0x000F;
    internal const uint WmSize = 0x0005;
    internal const uint WmMove = 0x0003;
    internal const uint WmKeyDown = 0x0100;
    internal const uint WmChar = 0x0102;
    internal const uint WmImeStartComposition = 0x010D;
    internal const uint WmImeEndComposition = 0x010E;
    internal const uint WmImeComposition = 0x010F;
    internal const uint GcsCompStr = 0x0008;
    internal const uint GcsResultStr = 0x0800;
    internal const uint WmSetFocus = 0x0007;
    internal const uint WmKillFocus = 0x0008;
    internal const uint WmShowWindow = 0x0018;
    internal const uint WmSetCursor = 0x0020;
    internal const uint WmMouseMove = 0x0200;
    internal const uint WmMouseLeave = 0x02A3;
    internal const uint WmCaptureChanged = 0x0215;
    internal const uint WmLButtonDown = 0x0201;
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmRButtonDown = 0x0204;
    internal const uint WmRButtonUp = 0x0205;
    internal const uint WmMButtonDown = 0x0207;
    internal const uint WmMButtonUp = 0x0208;
    internal const uint WmMouseWheel = 0x020A;
    internal const uint WmMouseHWheel = 0x020E;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint WmEraseBackground = 0x0014;
    internal const uint WmUser = 0x0400;
    internal const int HtClient = 1;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr LoadCursorW(IntPtr instance, IntPtr cursorName);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UpdateWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool PeekMessageW(
        out Message message,
        IntPtr window,
        uint filterMin,
        uint filterMax,
        uint removeMessage);

    [DllImport("user32.dll")]
    internal static extern uint MsgWaitForMultipleObjectsEx(
        uint handleCount,
        IntPtr handles,
        uint timeoutMilliseconds,
        uint wakeMask,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DispatchMessageW(ref Message message);

    [DllImport("user32.dll")]
    internal static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

    [DllImport("user32.dll")]
    internal static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool GetClientRect(IntPtr window, out Rect rectangle);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

    [DllImport("user32.dll")]
    internal static extern bool ScreenToClient(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    internal static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetCapture(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern bool TrackMouseEvent(ref TrackMouseEvent track);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool AdjustWindowRectEx(
        ref Rect rectangle,
        uint style,
        bool menu,
        uint extendedStyle);

    [DllImport("user32.dll")]
    internal static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SystemParametersInfoW(
        uint action,
        uint parameter,
        ref Rect value,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("imm32.dll")]
    internal static extern IntPtr ImmGetContext(IntPtr window);

    [DllImport("imm32.dll")]
    internal static extern bool ImmReleaseContext(IntPtr window, IntPtr context);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ImmGetCompositionStringW(
        IntPtr context,
        uint index,
        IntPtr buffer,
        int byteCount);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool BitBlt(
        IntPtr destinationContext,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr sourceContext,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [DllImport("winmm.dll")]
    internal static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    internal static extern uint timeEndPeriod(uint period);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WindowProcedure(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}
