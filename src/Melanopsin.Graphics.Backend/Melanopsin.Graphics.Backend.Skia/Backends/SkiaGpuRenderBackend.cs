// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Rendering;
using Melanopsin.Graphics.Text.Fonts;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

/// <summary>
/// Optional OpenGL backend: WGL on Windows, EGL on Linux. Render layers are
/// composed into GPU-backed Skia surfaces; callers can request an explicit BGRA
/// copy when a platform cannot consume the native surface.
/// </summary>
public sealed class SkiaGpuRenderBackend : IRenderBackend {
    private readonly FontFace _font;
    private readonly SKTypeface _typeface;
    private readonly ISkiaGpuContext _glContext;
    private readonly SkiaRasterCacheOptions _rasterCacheOptions;
    private int _disposed;
    private Exception? _cleanupFailure;

    public SkiaGpuRenderBackend(
        FontFace font,
        SkiaRasterCacheOptions rasterCacheOptions = default)
        : this(font, IntPtr.Zero, rasterCacheOptions) {
    }

    /// <summary>
    /// Creates an OpenGL backend using an existing native display connection. On
    /// Wayland this must be the <c>wl_display</c> the window host uses, so EGL
    /// surfaces share its connection and event queue. The caller keeps ownership.
    /// </summary>
    public SkiaGpuRenderBackend(
        FontFace font,
        IntPtr nativeDisplay,
        SkiaRasterCacheOptions rasterCacheOptions = default) {
        ArgumentNullException.ThrowIfNull(font);
        _rasterCacheOptions = rasterCacheOptions == default
            ? SkiaRasterCacheOptions.Default
            : rasterCacheOptions;
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
            _glContext,
            _rasterCacheOptions);
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
