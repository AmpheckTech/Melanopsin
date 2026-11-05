// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed partial class WglContext {
    public SkiaGpuRenderTargetHandle CreateTextureTarget(
        int width,
        int height) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MakeCurrent();
        var storageWidth = SkiaTextureCapacity.Get(width);
        var storageHeight = SkiaTextureCapacity.Get(height);
        WglNative.glGenTextures(1, out var texture);
        WglNative.glBindTexture(WglNative.GlTexture2D, texture);
        WglNative.glTexParameteri(
            WglNative.GlTexture2D,
            WglNative.GlTextureMinFilter,
            (int)WglNative.GlLinear);
        WglNative.glTexParameteri(
            WglNative.GlTexture2D,
            WglNative.GlTextureMagFilter,
            (int)WglNative.GlLinear);
        WglNative.glTexImage2D(
            WglNative.GlTexture2D,
            level: 0,
            internalFormat: (int)WglNative.GlRgba8,
            storageWidth,
            storageHeight,
            border: 0,
            WglNative.GlBgra,
            WglNative.GlUnsignedByte,
            IntPtr.Zero);
        _glGenFramebuffers(1, out var framebuffer);
        _glBindFramebuffer(WglNative.GlFramebuffer, framebuffer);
        _glFramebufferTexture2D(
            WglNative.GlFramebuffer,
            WglNative.GlColorAttachment0,
            WglNative.GlTexture2D,
            texture,
            level: 0);
        var status = _glCheckFramebufferStatus(WglNative.GlFramebuffer);
        _glBindFramebuffer(WglNative.GlFramebuffer, 0);
        if (status != WglNative.GlFramebufferComplete) {
            DeleteTextureTarget(new SkiaGpuRenderTargetHandle(
                texture,
                framebuffer,
                storageWidth,
                storageHeight));
            throw new InvalidOperationException(
                $"OpenGL framebuffer is incomplete (0x{status:X8}).");
        }

        return new SkiaGpuRenderTargetHandle(
            texture,
            framebuffer,
            storageWidth,
            storageHeight);
    }

    public SkiaGpuRenderTargetHandle ResizeTextureTarget(
        SkiaGpuRenderTargetHandle target,
        int width,
        int height) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MakeCurrent();
        if (width <= target.Width && height <= target.Height) {
            return target;
        }

        var storageWidth = Math.Max(target.Width, SkiaTextureCapacity.Get(width));
        var storageHeight = Math.Max(target.Height, SkiaTextureCapacity.Get(height));
        WglNative.glBindTexture(WglNative.GlTexture2D, target.TextureId);
        WglNative.glTexImage2D(
            WglNative.GlTexture2D,
            level: 0,
            internalFormat: (int)WglNative.GlRgba8,
            storageWidth,
            storageHeight,
            border: 0,
            WglNative.GlBgra,
            WglNative.GlUnsignedByte,
            IntPtr.Zero);
        WglNative.glBindTexture(WglNative.GlTexture2D, 0);

        _glBindFramebuffer(WglNative.GlFramebuffer, target.FramebufferId);
        var status = _glCheckFramebufferStatus(WglNative.GlFramebuffer);
        _glBindFramebuffer(WglNative.GlFramebuffer, 0);
        if (status != WglNative.GlFramebufferComplete) {
            throw new InvalidOperationException(
                $"OpenGL framebuffer is incomplete after resize (0x{status:X8}).");
        }

        return target with {
            Width = storageWidth,
            Height = storageHeight,
        };
    }

    public void DeleteTextureTarget(SkiaGpuRenderTargetHandle target) {
        if (_disposed) {
            return;
        }

        MakeCurrent();
        var framebuffer = target.FramebufferId;
        var texture = target.TextureId;
        _glDeleteFramebuffers(1, ref framebuffer);
        WglNative.glDeleteTextures(1, ref texture);
    }

    public void UploadTexture(
        SkiaGpuRenderTargetHandle target,
        ReadOnlyMemory<byte> pixels,
        int width,
        int height) {
        if (pixels.Length / 4 < (long)width * height) {
            throw new ArgumentException(
                "The upload buffer is smaller than the texture.",
                nameof(pixels));
        }

        MakeCurrent();
        WglNative.glBindTexture(WglNative.GlTexture2D, target.TextureId);
        byte[] storage;
        var offset = 0;
        if (MemoryMarshal.TryGetArray(pixels, out var segment) &&
            segment.Array is not null) {
            storage = segment.Array;
            offset = segment.Offset;
        } else {
            storage = pixels.ToArray();
        }

        var handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
        try {
            WglNative.glTexSubImage2D(
                WglNative.GlTexture2D,
                level: 0,
                xOffset: 0,
                yOffset: 0,
                width,
                height,
                WglNative.GlBgra,
                WglNative.GlUnsignedByte,
                IntPtr.Add(handle.AddrOfPinnedObject(), offset));
        } finally {
            handle.Free();
            WglNative.glBindTexture(WglNative.GlTexture2D, 0);
        }
    }

}
