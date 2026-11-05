// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal interface ISkiaGpuContext : IDisposable {
    GRContext GpuContext { get; }

    SkiaGpuRenderTargetHandle CreateTextureTarget(
        int width,
        int height);

    SkiaGpuRenderTargetHandle ResizeTextureTarget(
        SkiaGpuRenderTargetHandle target,
        int width,
        int height);

    void DeleteTextureTarget(SkiaGpuRenderTargetHandle target);

    void UploadTexture(
        SkiaGpuRenderTargetHandle target,
        ReadOnlyMemory<byte> pixels,
        int width,
        int height);

    bool TryPresent(
        IntPtr nativeWindow,
        SkiaGpuRenderTargetHandle target,
        int width,
        int height);

    void ReleaseNativeSurface(IntPtr nativeWindow);

    void MakeCurrent();
}

internal readonly record struct SkiaGpuRenderTargetHandle(
    uint TextureId,
    uint FramebufferId,
    int Width,
    int Height);
