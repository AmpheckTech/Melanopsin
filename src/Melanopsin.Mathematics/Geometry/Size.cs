// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A non-negative two-dimensional extent.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Size {
    /// <summary>
    /// Creates a size from finite non-negative extents.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is not finite or is negative.
    /// </exception>
    public Size(float width, float height) {
        if (!float.IsFinite(width) || width < 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!float.IsFinite(height) || height < 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public static Size Empty => default;

    public float Width { get; }

    public float Height { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
