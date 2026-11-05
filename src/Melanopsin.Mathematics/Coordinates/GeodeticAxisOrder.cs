// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Specifies the order of the horizontal axes in an external geodetic
/// coordinate tuple.
/// </summary>
/// <remarks>
/// <see cref="GeodeticCoordinate"/> always exposes named latitude, longitude,
/// and height properties. Axis order matters when exchanging tuples with
/// serialization formats, databases, or authority APIs.
/// </remarks>
public enum GeodeticAxisOrder {
    /// <summary>Latitude precedes longitude.</summary>
    LatitudeLongitude = 0,

    /// <summary>Longitude precedes latitude.</summary>
    LongitudeLatitude = 1,
}
