// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using Melanopsin.UI;

namespace Melanopsin.UI.Platform.Win32;

internal sealed partial class Win32Window {
    public void Present(RenderFrameLease frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!frame.IsValid) {
            throw new ArgumentException(
                "The render frame lease is stale.",
                nameof(frame));
        }

        var surface = frame.Surface;
        if (frame.VisualAccessibility == default &&
            surface is INativePresentationSurface nativeSurface) {
            if (nativeSurface.TryPresentNative(Handle)) {
                SetNativeSurface(nativeSurface);
                return;
            }

            ReleaseFailedNativePresentation(nativeSurface);
        } else {
            ReleaseNativeSurface();
        }

        if (surface is not ICpuReadableSurface cpuSurface) {
            throw new NotSupportedException(
                "The render surface cannot be presented to Win32.");
        }

        if (surface.PixelFormat != RenderPixelFormat.Bgra8888Premultiplied) {
            throw new NotSupportedException(
                $"Win32 presenter requires BGRA8888 frames, got {surface.PixelFormat}.");
        }

        if (_bitmapBits == IntPtr.Zero || _pixelSize != surface.Size) {
            RecreateSurface(surface.Size);
        }

        var rowBytes = checked(surface.Size.Width * 4);
        CopyPixels(cpuSurface, frame.Damage, frame.VisualAccessibility, rowBytes);
        PaintNow(frame.Damage);
    }

    public void Paint(IntPtr deviceContext) {
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

    private unsafe void CopyPixels(
        ICpuReadableSurface surface,
        DamageRegion damage,
        AccessibilityVisualOptions visualAccessibility,
        int rowBytes) {
        var destination = new Span<byte>(
            (void*)_bitmapBits,
            checked(rowBytes * surface.Size.Height));
        if (!surface.TryCopyPixels(
                damage,
                visualAccessibility,
                destination,
                out _)) {
            throw new InvalidOperationException(
                "The render surface could not copy its pixels.");
        }
    }

    private void SetNativeSurface(INativePresentationSurface surface) {
        if (!ReferenceEquals(_nativeSurface, surface)) {
            ReleaseNativeSurface();
            _nativeSurface = surface;
        }
    }

    private void ReleaseNativeSurface() {
        if (_nativeSurface is { } surface) {
            _nativeSurface = null;
            surface.ReleaseNativeTarget(Handle);
        }
    }

    private void ReleaseFailedNativePresentation(
        INativePresentationSurface attempted) {
        if (!ReferenceEquals(_nativeSurface, attempted)) {
            attempted.ReleaseNativeTarget(Handle);
        }

        ReleaseNativeSurface();
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
