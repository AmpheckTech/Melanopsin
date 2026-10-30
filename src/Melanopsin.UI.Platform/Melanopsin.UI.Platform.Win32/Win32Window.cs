using System.Runtime.InteropServices;
using Melanopsin.UI;
using Melanopsin.UI.Input;

namespace Melanopsin.UI.Platform.Win32;

internal sealed class Win32Window : IDisposable {
    private BitmapInfo _bitmapInfo;
    private PixelSize _pixelSize;
    private readonly HashSet<MouseButtonKind> _capturedButtons = [];
    private readonly IntPtr _deviceContext;
    private readonly IntPtr _memoryDeviceContext;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private IntPtr _bitmapBits;
    private IntPtr _cursorHandle;
    private IGpuFrameSurface? _gpuSurface;
    private bool _disposed;

    public Win32Window(IntPtr handle, WindowDescriptor windowDescriptor) {
        if (handle == IntPtr.Zero) {
            throw new ArgumentException("The Win32 window handle cannot be zero.", nameof(handle));
        }

        Handle = handle;
        Id = windowDescriptor.Id;
        Size = windowDescriptor.Size;
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

    public PixelSize Size { get; private set; }

    public int PositionX { get; private set; }

    public int PositionY { get; private set; }

    public bool IsTrackingMouse { get; set; }

    public bool IsComposing { get; set; }

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

    public void Present(RenderFrame frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        _gpuSurface = frame.GpuSurface;
        if (_gpuSurface?.TryPresent(Handle) == true) {
            return;
        }

        if (_gpuSurface is not null) {
            var pixels = frame.Pixels.Span;
            if (_gpuSurface.TryPresentPixels(
                    Handle,
                    pixels,
                    frame.Stride)) {
                return;
            }
        }

        _gpuSurface = null;
        if (frame.PixelFormat != RenderPixelFormat.Bgra8888Premultiplied) {
            throw new NotSupportedException(
                $"Win32 presenter requires BGRA8888 frames, got {frame.PixelFormat}.");
        }

        if (_bitmapBits == IntPtr.Zero || _pixelSize != frame.Descriptor.Size) {
            RecreateSurface(frame.Descriptor.Size);
        }

        var rowBytes = frame.Descriptor.Size.Width * 4;
        CopyPixels(frame, rowBytes);
        PaintNow(frame.Damage);
    }

    public void Paint(IntPtr deviceContext) {
        if (_gpuSurface?.TryPresent(Handle) == true) {
            return;
        }

        if (_disposed || _bitmapBits == IntPtr.Zero) {
            return;
        }

        Paint(deviceContext, DamageRegion.Full(_pixelSize));
    }

    public void Paint(IntPtr deviceContext, DamageRegion damage) {
        ArgumentNullException.ThrowIfNull(damage);
        if (_disposed || _bitmapBits == IntPtr.Zero || damage.IsEmpty) {
            return;
        }

        foreach (var rect in damage.Rects) {
            _ = Win32Native.BitBlt(
                deviceContext,
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                _memoryDeviceContext,
                rect.X,
                rect.Y,
                Win32Native.Srccopy);
        }
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

        _capturedButtons.Clear();
        _bitmapBits = IntPtr.Zero;
        _gpuSurface = null;
    }

    private void PaintNow(DamageRegion damage) {
        if (_deviceContext != IntPtr.Zero) {
            Paint(_deviceContext, damage);
            return;
        }

        var deviceContext = Win32Native.GetDC(Handle);
        if (deviceContext == IntPtr.Zero) {
            return;
        }

        try {
            Paint(deviceContext, damage);
        } finally {
            Win32Native.ReleaseDC(Handle, deviceContext);
        }
    }

    private unsafe void CopyPixels(RenderFrame frame, int rowBytes) {
        var destination = new Span<byte>(
            (void*)_bitmapBits,
            checked(rowBytes * frame.Descriptor.Size.Height));
        BgraFrameCopier.Copy(
            frame.Pixels.Span,
            frame.Stride,
            destination,
            rowBytes,
            frame.Damage);
    }

    private void RecreateSurface(PixelSize size) {
        if (_bitmap != IntPtr.Zero) {
            _ = Win32Native.SelectObject(_memoryDeviceContext, _previousBitmap);
            _ = Win32Native.DeleteObject(_bitmap);
            _bitmap = IntPtr.Zero;
        }

        _bitmapBits = IntPtr.Zero;
        _pixelSize = size;
        _bitmapInfo = CreateBitmapInfo(size);
        _bitmap = Win32Native.CreateDIBSection(
            _deviceContext,
            ref _bitmapInfo,
            Win32Native.DibRgbColors,
            out _bitmapBits,
            IntPtr.Zero,
            0);
        if (_bitmap == IntPtr.Zero || _bitmapBits == IntPtr.Zero) {
            if (_bitmap != IntPtr.Zero) {
                _ = Win32Native.DeleteObject(_bitmap);
                _bitmap = IntPtr.Zero;
            }

            _bitmapBits = IntPtr.Zero;
            throw new InvalidOperationException(
                "Win32 could not create the presentation DIB section.");
        }

        _previousBitmap = Win32Native.SelectObject(_memoryDeviceContext, _bitmap);
    }

    private static BitmapInfo CreateBitmapInfo(PixelSize size) {
        return new BitmapInfo {
            Header = new BitmapInfoHeader {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = size.Width,
                Height = -size.Height,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
                ImageSize = (uint)(size.Width * size.Height * 4),
                XPelsPerMeter = 0,
                YPelsPerMeter = 0,
                ColorsUsed = 0,
                ColorsImportant = 0,
            },
            Colors = 0,
        };
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
