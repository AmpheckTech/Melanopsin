// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A solid sphere in three-dimensional space.
/// </summary>
public readonly record struct Sphere3 {
    public Sphere3(Vector3 center, float radius) {
        if (!center.IsFinite || !float.IsFinite(radius) || radius < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                "The center must be finite and the radius must be finite and non-negative.");
        }

        Center = center;
        Radius = radius;
    }

    public Vector3 Center { get; }

    public float Radius { get; }

    /// <summary>
    /// Gets the diameter. The result is positive infinity when the
    /// mathematical diameter exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Diameter => (float)(2d * Radius);

    /// <summary>
    /// Gets the surface area, evaluated in double precision. The result is
    /// positive infinity when it exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float SurfaceArea =>
        (float)(4 * Constants.Pi * (double)Radius * Radius);

    /// <summary>
    /// Gets the volume, evaluated in double precision. The result is positive
    /// infinity when it exceeds <see cref="float.MaxValue"/>.
    /// </summary>
    public float Volume =>
        (float)((4.0 / 3.0) * Constants.Pi * (double)Radius * Radius * Radius);

    public bool Contains(Vector3 point) {
        return GeometryPredicates.DistanceSquared(Center, point) <=
            ((double)Radius * Radius);
    }

    /// <summary>
    /// Gets the finite axis-aligned bounds. Coordinates are saturated at the
    /// finite single-precision limits if the exact bound lies outside the
    /// representable coordinate range.
    /// </summary>
    public Aabb3 Bounds => new(
        new Vector3(
            FloatingPointMath.SaturatingAdd(Center.X, -Radius),
            FloatingPointMath.SaturatingAdd(Center.Y, -Radius),
            FloatingPointMath.SaturatingAdd(Center.Z, -Radius)),
        new Vector3(
            FloatingPointMath.SaturatingAdd(Center.X, Radius),
            FloatingPointMath.SaturatingAdd(Center.Y, Radius),
            FloatingPointMath.SaturatingAdd(Center.Z, Radius)));

    public Vector3 ClosestPoint(Vector3 point) {
        var offsetX = (double)point.X - Center.X;
        var offsetY = (double)point.Y - Center.Y;
        var offsetZ = (double)point.Z - Center.Z;
        var distanceSquared = (offsetX * offsetX) +
            (offsetY * offsetY) +
            (offsetZ * offsetZ);
        if (distanceSquared <= ((double)Radius * Radius)) {
            return point;
        }

        var scale = Radius / Math.Sqrt(distanceSquared);
        return new Vector3(
            (float)(Center.X + (offsetX * scale)),
            (float)(Center.Y + (offsetY * scale)),
            (float)(Center.Z + (offsetZ * scale)));
    }
}
