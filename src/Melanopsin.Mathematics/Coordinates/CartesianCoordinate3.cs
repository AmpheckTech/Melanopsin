// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A finite Cartesian coordinate in three-dimensional double precision.
/// </summary>
/// <remarks>
/// The type does not impose a physical unit or a handedness. Conversions in
/// <see cref="CoordinateTransforms"/> make their handedness and angle
/// conventions explicit at each call site.
/// </remarks>
public readonly record struct CartesianCoordinate3 {
    public CartesianCoordinate3(double x, double y, double z) {
        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z)) {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double Radius => ElementaryFunctions.Hypot(X, Y, Z);

    public static CartesianCoordinate3 FromEcef(EcefCoordinate coordinate) {
        return new CartesianCoordinate3(coordinate.X, coordinate.Y, coordinate.Z);
    }

    public EcefCoordinate ToEcef() {
        return new EcefCoordinate(X, Y, Z);
    }
}
