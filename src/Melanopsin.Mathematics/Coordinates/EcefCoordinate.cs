// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// An Earth-centered, Earth-fixed Cartesian coordinate in a right-handed
/// frame: X intersects the equator at the prime meridian, Z follows the
/// ellipsoid's north axis, and Y = Z cross X.
/// </summary>
public readonly record struct EcefCoordinate {
    public EcefCoordinate(double x, double y, double z) {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }
}
