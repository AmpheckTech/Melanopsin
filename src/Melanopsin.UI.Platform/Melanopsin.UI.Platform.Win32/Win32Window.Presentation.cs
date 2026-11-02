using System.Runtime.InteropServices;
using Melanopsin.UI;

namespace Melanopsin.UI.Platform.Win32;

internal sealed partial class Win32Window {
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
                ImageSize = checked((uint)((long)size.Width * size.Height * 4)),
                XPelsPerMeter = 0,
                YPelsPerMeter = 0,
                ColorsUsed = 0,
                ColorsImportant = 0,
            },
            Colors = 0,
        };
    }
}
