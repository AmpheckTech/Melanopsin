// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A spherical coordinate with radial distance, polar angle, and azimuth.
/// </summary>
/// <remarks>
/// The polar angle is measured from the positive Z axis and is constrained to
/// [0, pi]. Azimuth is measured from the positive X axis in radians. At the
/// poles azimuth does not affect the Cartesian position.
/// </remarks>
public readonly record struct SphericalCoordinate {
    public SphericalCoordinate(
        double radialDistance,
        double polarAngleRadians,
        double azimuthRadians) {
        if (!double.IsFinite(radialDistance) || radialDistance < 0) {
            throw new ArgumentOutOfRangeException(nameof(radialDistance));
        }

        if (!double.IsFinite(polarAngleRadians) ||
            polarAngleRadians < 0 ||
            polarAngleRadians > Constants.Pi) {
            throw new ArgumentOutOfRangeException(nameof(polarAngleRadians));
        }

        if (!double.IsFinite(azimuthRadians)) {
            throw new ArgumentOutOfRangeException(nameof(azimuthRadians));
        }

        RadialDistance = radialDistance;
        PolarAngleRadians = polarAngleRadians;
        AzimuthRadians = azimuthRadians;
    }

    public double RadialDistance { get; }

    public double PolarAngleRadians { get; }

    public double AzimuthRadians { get; }
}
