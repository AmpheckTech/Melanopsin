// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Mathematics.Coordinates;

namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// A unit-speed geometrical ray represented in an explicitly supplied
/// coordinate frame.
/// </summary>
/// <remarks>
/// The ray is <c>origin + distance * direction</c>. Direction is normalized at
/// construction, so <c>distance</c> is measured in the same unit as the origin
/// and frame. The ray does not embed a frame identity; every operation that
/// changes frames requires the source and destination frames explicitly.
/// Optical properties such as wavelength, intensity, phase, and polarization
/// are intentionally outside this geometrical type.
/// </remarks>
public readonly partial record struct OpticalRay {
    private OpticalRay(
        CartesianCoordinate3 origin,
        Vector3<double> direction,
        double inverseLength) {
        Origin = origin;
        Direction = new Vector3<double>(
            direction.X * inverseLength,
            direction.Y * inverseLength,
            direction.Z * inverseLength);
    }

    public OpticalRay(
        CartesianCoordinate3 origin,
        Vector3<double> direction) {
        if (!CartesianVector3Math.TryNormalize(
            direction,
            out _,
            out var inverseLength)) {
            throw new ArgumentException(
                "The ray direction must be finite and non-zero.",
                nameof(direction));
        }

        Origin = origin;
        Direction = new Vector3<double>(
            direction.X * inverseLength,
            direction.Y * inverseLength,
            direction.Z * inverseLength);
    }

    public CartesianCoordinate3 Origin { get; }

    public Vector3<double> Direction { get; }

    public static bool TryCreate(
        CartesianCoordinate3 origin,
        Vector3<double> direction,
        out OpticalRay ray) {
        return TryCreate(origin, direction, out ray, out _);
    }

    /// <summary>
    /// Gets a point at a signed distance along the normalized ray direction.
    /// </summary>
    public CartesianCoordinate3 PointAt(double distance) {
        if (!TryPointAt(distance, out var result)) {
            throw new ArithmeticException(
                "The point on the ray is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to get a point at a signed distance along the normalized ray
    /// direction.
    /// </summary>
    public bool TryPointAt(
        double distance,
        out CartesianCoordinate3 result) {
        return TryPointAt(distance, out result, out _);
    }

    /// <summary>
    /// Constructs a ray-local frame whose positive Z axis follows the ray.
    /// </summary>
    /// <remarks>
    /// The X-axis hint fixes the transverse orientation. A right-handed frame
    /// has its local Y axis equal to <c>Z cross X</c>; a left-handed frame has
    /// <c>Y = X cross Z</c>. The frame origin is this ray's origin.
    /// </remarks>
    public CoordinateFrame3 CreateLocalFrame(
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return CoordinateFrame3.CreateFromZAxis(
            Origin,
            Direction,
            transverseXAxisHint,
            handedness);
    }

    /// <summary>
    /// Attempts to construct a ray-local frame whose positive Z axis follows
    /// the ray.
    /// </summary>
    public bool TryCreateLocalFrame(
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreateLocalFrame(
            transverseXAxisHint,
            handedness,
            out frame,
            out _);
    }

    /// <summary>
    /// Constructs a ray-local frame at a point on the ray.
    /// </summary>
    public CoordinateFrame3 CreateLocalFrameAt(
        double distance,
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return CoordinateFrame3.CreateFromZAxis(
            PointAt(distance),
            Direction,
            transverseXAxisHint,
            handedness);
    }

    /// <summary>
    /// Attempts to construct a ray-local frame at a point on the ray.
    /// </summary>
    public bool TryCreateLocalFrameAt(
        double distance,
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreateLocalFrameAt(
            distance,
            transverseXAxisHint,
            handedness,
            out frame,
            out _);
    }

    /// <summary>
    /// Transforms the ray between two frames that share the same reference
    /// frame.
    /// </summary>
    public OpticalRay Transform(
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        if (!TryTransform(source, target, out var result)) {
            throw new ArithmeticException(
                "The transformed ray is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform the ray between two frames that share the same
    /// reference frame.
    /// </summary>
    public bool TryTransform(
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out OpticalRay ray) {
        return TryTransform(source, target, out ray, out _);
    }
}
