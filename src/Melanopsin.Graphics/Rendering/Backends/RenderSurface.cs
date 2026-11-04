namespace Melanopsin.Graphics.Rendering;

public interface IRenderSurface {
    PixelSize Size { get; }

    RenderPixelFormat PixelFormat { get; }

    int Stride { get; }
}

public interface ICpuReadableSurface : IRenderSurface {
    /// <summary>
    /// Refreshes <paramref name="damage"/> and copies a complete BGRA snapshot
    /// into caller-owned storage. The destination is never retained.
    /// </summary>
    bool TryCopyPixels(
        DamageRegion damage,
        AccessibilityVisualOptions visualAccessibility,
        Span<byte> destination,
        out int stride);
}

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
    /// Releases resources associated with the native target binding.
    /// </summary>
    void ReleaseNativeTarget(IntPtr target);
}
