// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A local north-east-down (NED) Cartesian coordinate in metres.
/// </summary>
/// <remarks>
/// NED is a right-handed local navigation frame. Positive down is opposite
/// the local up direction used by <see cref="EnuCoordinate"/>.
/// </remarks>
public readonly record struct NedCoordinate {
    public NedCoordinate(double north, double east, double down) {
        if (!double.IsFinite(north) ||
            !double.IsFinite(east) ||
            !double.IsFinite(down)) {
            throw new ArgumentException(
                "NED coordinates must be finite.");
        }

        North = north;
        East = east;
        Down = down;
    }

    public double North { get; }

    public double East { get; }

    public double Down { get; }

    public EnuCoordinate ToEnu() => new(East, North, -Down);

    public static NedCoordinate FromEnu(EnuCoordinate coordinate) {
        return new NedCoordinate(coordinate.North, coordinate.East, -coordinate.Up);
    }
}
