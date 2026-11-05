// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// An axis-aligned rectangle in two-dimensional space, represented by minimum
/// and maximum coordinate bounds.
/// </summary>
/// <remarks>
/// A rectangle does not carry a raster origin or Y-axis convention. Use
/// raster-specific extension APIs when physical top and bottom semantics are
/// required.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rect {
    /// <summary>
    /// Creates a rectangle from finite position and extent components.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is not finite, or a width or height is negative.
    /// </exception>
    public Rect(float minX, float minY, float width, float height) {
        if (!float.IsFinite(minX)) {
            throw new ArgumentOutOfRangeException(nameof(minX));
        }

        if (!float.IsFinite(minY)) {
            throw new ArgumentOutOfRangeException(nameof(minY));
        }

        if (!float.IsFinite(width) || width < 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!float.IsFinite(height) || height < 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        MinX = minX;
        MinY = minY;
        Width = width;
        Height = height;
    }

    public Rect(Point position, Size size)
        : this(position.X, position.Y, size.Width, size.Height) {
    }

    public float MinX { get; }

    public float MinY { get; }

    public float Width { get; }

    public float Height { get; }

    /// <summary>
    /// Gets the right bound, saturated at the finite single-precision maximum
    /// when the mathematical bound is outside the representable coordinate range.
    /// </summary>
    public float MaxX => FloatingPointMath.SaturatingAdd(MinX, Width);

    /// <summary>
    /// Gets the bottom bound, saturated at the finite single-precision maximum
    /// when the mathematical bound is outside the representable coordinate range.
    /// </summary>
    public float MaxY => FloatingPointMath.SaturatingAdd(MinY, Height);

    public Point Position => new(MinX, MinY);

    public Size Size => new(Width, Height);

    public Point Center => new(
        FloatingPointMath.SaturatingAdd(MinX, (double)Width * 0.5),
        FloatingPointMath.SaturatingAdd(MinY, (double)Height * 0.5));

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(float x, float y) {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    public bool Contains(Point point) {
        return Contains(point.X, point.Y);
    }

    public bool IntersectsWith(Rect other) {
        return !IsEmpty &&
               !other.IsEmpty &&
               other.MinX < MaxX &&
               other.MaxX > MinX &&
               other.MinY < MaxY &&
               other.MaxY > MinY;
    }

    public Rect Intersect(Rect other) {
        var minX = Math.Max(MinX, other.MinX);
        var minY = Math.Max(MinY, other.MinY);
        var maxX = Math.Min(MaxX, other.MaxX);
        var maxY = Math.Min(MaxY, other.MaxY);
        return new Rect(
            minX,
            minY,
            FloatingPointMath.SaturatingConvert(Math.Max(0, (double)maxX - minX)),
            FloatingPointMath.SaturatingConvert(Math.Max(0, (double)maxY - minY)));
    }

    public Rect Union(Rect other) {
        if (IsEmpty) {
            return other;
        }

        if (other.IsEmpty) {
            return this;
        }

        var minX = Math.Min(MinX, other.MinX);
        var minY = Math.Min(MinY, other.MinY);
        var maxX = Math.Max(MaxX, other.MaxX);
        var maxY = Math.Max(MaxY, other.MaxY);
        return new Rect(
            minX,
            minY,
            FloatingPointMath.SaturatingConvert((double)maxX - minX),
            FloatingPointMath.SaturatingConvert((double)maxY - minY));
    }

    /// <summary>
    /// Expands the rectangle by a finite non-negative amount. Bounds and
    /// extents saturate at the finite single-precision limits.
    /// </summary>
    public Rect Inflate(float amount) {
        if (!float.IsFinite(amount) || amount < 0) {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount == 0) {
            return this;
        }

        return new Rect(
            FloatingPointMath.SaturatingAdd(MinX, -(double)amount),
            FloatingPointMath.SaturatingAdd(MinY, -(double)amount),
            FloatingPointMath.SaturatingConvert((double)Width + (2d * amount)),
            FloatingPointMath.SaturatingConvert((double)Height + (2d * amount)));
    }
}
