namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Backend-owned GPU presentation surface. Iterator pixels must not be retained
/// after the next render of the producing target.
/// </summary>
public interface IGpuFrameSurface {
    PixelSize Size { get; }

    RenderPixelFormat PixelFormat { get; }

    bool TryPresent(IntPtr nativeWindow);

    bool TryPresentPixels(
        IntPtr nativeWindow,
        ReadOnlySpan<byte> pixels,
        int stride);

    /// <summary>
    /// Releases cached presentation resources associated with a native window.
    /// The default implementation is a no-op for presenters that do not cache
    /// native window resources.
    /// </summary>
    void ReleasePresentSurface(IntPtr nativeWindow) {
    }

    ReadOnlyMemory<byte> ReadPixels();
}
