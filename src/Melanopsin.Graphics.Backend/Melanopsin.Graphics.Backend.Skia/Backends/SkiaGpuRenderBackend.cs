using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.Graphics.Rendering;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

/// <summary>
/// Optional OpenGL backend. On Windows it uses WGL; on Linux it uses EGL with
/// Wayland or surfaceless display support. Render layers are composed into
/// GPU-backed Skia surfaces and copied into a native presentation surface.
/// Platforms that cannot consume the GPU surface fall back to a BGRA readback.
/// </summary>
public sealed class SkiaGpuRenderBackend : IRenderBackend {
    private readonly FontFace _font;
    private readonly SKTypeface _typeface;
    private readonly ISkiaGpuContext _glContext;
    private int _disposed;
    private Exception? _cleanupFailure;

    public SkiaGpuRenderBackend(FontFace font)
        : this(font, nativeDisplay: IntPtr.Zero) {
    }

    /// <summary>
    /// Creates an OpenGL backend using an existing native display connection.
    /// On Linux/Wayland this must be the <c>wl_display</c> used by the window
    /// host so EGL window surfaces share the same connection and event queue.
    /// The caller retains ownership of the native display.
    /// </summary>
    public SkiaGpuRenderBackend(FontFace font, IntPtr nativeDisplay) {
        ArgumentNullException.ThrowIfNull(font);
        _font = font;
        var glContext = CreateGpuContext(nativeDisplay);
        try {
            using var stream = font.OpenStream();
            _typeface = SKTypeface.FromStream(stream, font.FaceIndex) ??
                throw new InvalidOperationException(
                    $"Skia could not create a typeface from font " +
                    $"'{font.SourceName ?? font.FamilyName}'.");
            _glContext = glContext;
        } catch {
            glContext.Dispose();
            throw;
        }
    }

    public string Name => "Skia OpenGL GPU";

    public bool IsHardwareAccelerated => true;

    /// <summary>
    /// The failure observed during the most recent disposal. Disposal records
    /// this value instead of throwing so it cannot mask an active exception.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public IRenderTarget CreateTarget(RenderTargetDescriptor descriptor) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return new SkiaRenderTarget(
            descriptor,
            _font,
            _typeface,
            _glContext);
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        Exception? failure = null;
        SkiaCleanup.Capture(ref failure, _glContext.Dispose);
        SkiaCleanup.Capture(ref failure, _typeface.Dispose);
        Volatile.Write(ref _cleanupFailure, failure);
    }

    private static ISkiaGpuContext CreateGpuContext(IntPtr nativeDisplay) {
        if (OperatingSystem.IsWindows()) {
            return new WglContext();
        }

        if (OperatingSystem.IsLinux()) {
            return new EglGpuContext(nativeDisplay);
        }

        throw new PlatformNotSupportedException(
            "SkiaGpuRenderBackend requires Windows/WGL or Linux/EGL.");
    }
}
