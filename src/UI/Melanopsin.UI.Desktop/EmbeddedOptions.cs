// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Desktop;

public readonly record struct FontAsset {
    public FontAsset(string path, int faceIndex = 0) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);
        Path = path;
        FaceIndex = faceIndex;
    }

    public string Path { get; }

    public int FaceIndex { get; }
}

/// <summary>
/// Runtime memory policy for constrained devices. NativeAOT trimming and GC
/// limits are configured when the application is published.
/// </summary>
public sealed class EmbeddedOptions {
    /// <summary>
    /// Fonts explicitly made available for characters that are absent from the
    /// primary font. No host fonts are discovered unless they are listed here.
    /// </summary>
    public IList<FontAsset> FallbackFonts { get; } = new List<FontAsset>();

    public int ActiveFrameRate { get; set; } = 30;

    public int IdleFrameRate { get; set; } = 5;

    public long RasterCacheByteLimit { get; set; } = 8L * 1024 * 1024;

    public int RasterCacheEntryLimit { get; set; } = 32;
}
