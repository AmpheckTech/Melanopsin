using Melanopsin.UI;
using Melanopsin.UI.Input;

namespace Melanopsin.UI.Platform.Win32;

internal sealed partial class Win32Window : IDisposable {
    private BitmapInfo _bitmapInfo;
    private PixelSize _pixelSize;
    private readonly HashSet<MouseButtonKind> _capturedButtons = [];
    private readonly IntPtr _deviceContext;
    private readonly IntPtr _memoryDeviceContext;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private IntPtr _bitmapBits;
    private IntPtr _cursorHandle;
    private INativePresentationSurface? _nativeSurface;
    private bool _disposed;

    public Win32Window(IntPtr handle, WindowDescriptor windowDescriptor) {
        if (handle == IntPtr.Zero) {
            throw new ArgumentException("The Win32 window handle cannot be zero.", nameof(handle));
        }

        Handle = handle;
        Id = windowDescriptor.Id;
        Size = windowDescriptor.Size;
        OwnerId = windowDescriptor.OwnerId;
        IsModal = windowDescriptor.IsModal;
        _cursorHandle = LoadCursor(Cursor);
        _deviceContext = Win32Native.GetDC(Handle);
        _memoryDeviceContext = Win32Native.CreateCompatibleDC(_deviceContext);
        if (_memoryDeviceContext == IntPtr.Zero) {
            if (_deviceContext != IntPtr.Zero) {
                _ = Win32Native.ReleaseDC(Handle, _deviceContext);
            }

            throw new InvalidOperationException(
                "Win32 could not create a compatible memory device context.");
        }
    }

    public IntPtr Handle { get; }

    public WindowId Id { get; }

    public WindowId? OwnerId { get; }

    public bool IsModal { get; }

    public PixelSize Size { get; private set; }

    public int PositionX { get; private set; }

    public int PositionY { get; private set; }

    public bool IsTrackingMouse { get; set; }

    public bool IsComposing { get; set; }

    public bool ImeHasResult { get; set; }

    public bool IgnoreNextTextInput { get; set; }

    public bool HasMouseCapture => _capturedButtons.Count != 0;

    public CursorKind Cursor { get; private set; } = CursorKind.Auto;

    public IntPtr CursorHandle => _cursorHandle;

    public void SetCursor(CursorKind cursor) {
        Cursor = cursor;
        _cursorHandle = LoadCursor(cursor);
    }

    public void BeginMouseCapture(MouseButtonKind button) {
        if (button != MouseButtonKind.None) {
            _capturedButtons.Add(button);
        }
    }

    public bool EndMouseCapture(MouseButtonKind button) {
        _capturedButtons.Remove(button);
        return _capturedButtons.Count == 0;
    }

    public void ClearMouseCapture() {
        _capturedButtons.Clear();
    }

    public void UpdateSize(PixelSize size) {
        Size = size;
    }

    public bool UpdatePosition(int x, int y) {
        if (PositionX == x && PositionY == y) {
            return false;
        }

        PositionX = x;
        PositionY = y;
        return true;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        try {
            ReleaseNativeSurface();
        } catch (Exception exception) {
            failure = exception;
        }

        try {
            if (_deviceContext != IntPtr.Zero) {
                Win32Native.ReleaseDC(Handle, _deviceContext);
            }

            if (_memoryDeviceContext != IntPtr.Zero) {
                _ = Win32Native.SelectObject(_memoryDeviceContext, _previousBitmap);
            }

            if (_bitmap != IntPtr.Zero) {
                _ = Win32Native.DeleteObject(_bitmap);
                _bitmap = IntPtr.Zero;
            }

            if (_memoryDeviceContext != IntPtr.Zero) {
                _ = Win32Native.DeleteDC(_memoryDeviceContext);
            }
        } finally {
            _capturedButtons.Clear();
            _bitmapBits = IntPtr.Zero;
        }

        if (failure is not null) {
            throw failure;
        }
    }

    private static IntPtr LoadCursor(CursorKind cursor) {
        if (cursor == CursorKind.None) {
            return IntPtr.Zero;
        }

        var resource = cursor switch {
            CursorKind.Pointer or CursorKind.Grab or CursorKind.Grabbing => 32649,
            CursorKind.Text or CursorKind.VerticalText => 32513,
            CursorKind.Wait => 32514,
            CursorKind.Progress => 32650,
            CursorKind.Cell or CursorKind.Crosshair => 32515,
            CursorKind.Help => 32651,
            CursorKind.NoDrop or CursorKind.NotAllowed => 32648,
            CursorKind.Move or CursorKind.AllScroll or CursorKind.AllResize => 32646,
            CursorKind.ColResize or CursorKind.EResize or
                CursorKind.WResize or CursorKind.EwResize => 32644,
            CursorKind.RowResize or CursorKind.NResize or
                CursorKind.SResize or CursorKind.NsResize => 32645,
            CursorKind.NwResize or CursorKind.SeResize or
                CursorKind.NwseResize => 32642,
            CursorKind.NeResize or CursorKind.SwResize or
                CursorKind.NeswResize => 32643,
            _ => Win32Native.IdcArrow,
        };
        return Win32Native.LoadCursorW(IntPtr.Zero, new IntPtr(resource));
    }
}
