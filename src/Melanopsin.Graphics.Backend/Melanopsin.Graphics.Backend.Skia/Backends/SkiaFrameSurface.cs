// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaFrameSurface(SkiaRenderTarget owner) :
    ICpuReadableSurface,
    INativePresentationSurface {
    public PixelSize Size => owner.Descriptor.Size;

    public RenderPixelFormat PixelFormat =>
        RenderPixelFormat.Bgra8888Premultiplied;

    public int Stride => checked(Size.Width * 4);

    public bool TryCopyPixels(
        DamageRegion damage,
        AccessibilityVisualOptions visualAccessibility,
        Span<byte> destination,
        out int stride) {
        stride = Stride;
        return owner.TryCopyPixels(
            damage,
            visualAccessibility,
            destination,
            stride);
    }

    public bool TryPresentNative(IntPtr target) {
        return owner.TryPresentNative(target);
    }

    public void ReleaseNativeTarget(IntPtr target) {
        owner.ReleaseNativeTarget(target);
    }
}
