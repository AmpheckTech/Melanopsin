// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Backend.Skia;

/// <summary>
/// Budget for retained offscreen layers. Lower limits trade cache memory for
/// additional repainting.
/// </summary>
public readonly record struct SkiaRasterCacheOptions {
    public SkiaRasterCacheOptions(
        long byteLimit = 32L * 1024 * 1024,
        int entryLimit = 128,
        long entryByteLimit = 8L * 1024 * 1024) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryByteLimit);
        ByteLimit = byteLimit;
        EntryLimit = entryLimit;
        EntryByteLimit = entryByteLimit;
    }

    public long ByteLimit { get; }

    public int EntryLimit { get; }

    public long EntryByteLimit { get; }

    public static SkiaRasterCacheOptions Default { get; } = new();
}
