// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

public interface IRenderSurface {
    PixelSize Size { get; }

    RenderPixelFormat PixelFormat { get; }

    int Stride { get; }
}

public interface ICpuReadableSurface : IRenderSurface {
    /// <summary>
    /// Refreshes <paramref name="damage"/> and copies a complete BGRA snapshot
    /// into caller-owned storage. The destination must hold at least
    /// <see cref="IRenderSurface.Stride"/> times <see cref="IRenderSurface.Size"/>
    /// height bytes; <paramref name="stride"/> receives the row stride used.
    /// A false result does not represent a complete snapshot. The destination
    /// is never retained.
    /// </summary>
    bool TryCopyPixels(
        DamageRegion damage,
        AccessibilityVisualOptions visualAccessibility,
        Span<byte> destination,
        out int stride);
}

// TODO(DRM): Replace the boolean presentation result with a present ticket and
// release fence before exposing DMA-BUF scanout through this capability.
/// <summary>
/// Optional capability for surfaces that can be submitted directly to a
/// platform-native presentation target. The handle is opaque to graphics code.
/// </summary>
public interface INativePresentationSurface : IRenderSurface {
    /// <summary>
    /// Submits the surface to a native target. A successful result means the
    /// source can be reused immediately because it was copied synchronously or
    /// an independent asynchronous retention/fence has been established.
    /// A boolean result alone is not sufficient for DMA-BUF scanout; that path
    /// requires a release ticket before this capability can be exposed.
    /// </summary>
    bool TryPresentNative(IntPtr target);

    /// <summary>
    /// Releases resources associated with the native target binding. The
    /// operation is idempotent for the current EGL and WGL implementations.
    /// </summary>
    void ReleaseNativeTarget(IntPtr target);
}
