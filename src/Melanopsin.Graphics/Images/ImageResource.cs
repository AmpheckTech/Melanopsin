// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics;

/// <summary>
/// Encoded image bytes plus their declared media type. Decoding and pixel
/// preparation remain backend responsibilities; this type only carries the
/// encoded resource value.
/// </summary>
public sealed class ImageResource {
    public ImageResource(ReadOnlyMemory<byte> data, string? mediaType = null) {
        if (data.IsEmpty) {
            throw new ArgumentException(
                "An encoded image resource cannot be empty.",
                nameof(data));
        }

        Data = data;
        MediaType = string.IsNullOrWhiteSpace(mediaType)
            ? null
            : mediaType;
    }

    public ReadOnlyMemory<byte> Data { get; }

    public string? MediaType { get; }
}
