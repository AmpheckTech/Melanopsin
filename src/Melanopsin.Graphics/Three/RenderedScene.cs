// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Three;

/// <summary>
/// A BGRA premultiplied raster produced by <see cref="SoftwareSceneRenderer"/>.
/// The pixels remain valid until the same renderer renders again.
/// </summary>
public readonly record struct RenderedScene {
    public RenderedScene(PixelSize size, int stride, ReadOnlyMemory<byte> pixels) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        var minimumStride = checked(size.Width * 4);
        if (stride < minimumStride) {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                stride,
                "The stride must cover the full image width.");
        }

        if (pixels.Length < checked((long)stride * size.Height)) {
            throw new ArgumentException(
                "The pixel buffer is smaller than the rendered image.",
                nameof(pixels));
        }

        Size = size;
        Stride = stride;
        Pixels = pixels;
    }

    public PixelSize Size { get; }

    public int Stride { get; }

    public ReadOnlyMemory<byte> Pixels { get; }
}
