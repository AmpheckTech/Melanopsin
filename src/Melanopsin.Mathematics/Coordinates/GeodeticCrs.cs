// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A geodetic coordinate reference system with an ellipsoidal height axis.
/// </summary>
/// <remarks>
/// The coordinate values used by this library are latitude and longitude in
/// degrees plus ellipsoidal height in the datum ellipsoid's length unit. The
/// axis order is retained as metadata for external data exchange and does not
/// alter the named properties of <see cref="GeodeticCoordinate"/>.
/// </remarks>
public sealed record GeodeticCrs {
    /// <summary>
    /// Initializes a geodetic coordinate reference system.
    /// </summary>
    public GeodeticCrs(
        CoordinateReferenceSystemId id,
        string name,
        GeodeticDatum datum,
        GeodeticAxisOrder axisOrder =
            GeodeticAxisOrder.LatitudeLongitude) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException(
                "A coordinate reference system name is required.",
                nameof(name));
        }

        if (!Enum.IsDefined(axisOrder)) {
            throw new ArgumentOutOfRangeException(nameof(axisOrder));
        }

        Id = id;
        Name = name;
        Datum = datum;
        AxisOrder = axisOrder;
    }

    /// <summary>Gets EPSG:4979, WGS84 geographic 3D.</summary>
    public static GeodeticCrs Wgs84 { get; } = new(
        CoordinateReferenceSystemId.Epsg(4979),
        "WGS 84 geographic 3D",
        GeodeticDatum.Wgs84);

    /// <summary>Gets EPSG:4480, CGCS2000 geographic 3D.</summary>
    public static GeodeticCrs Cgcs2000 { get; } = new(
        CoordinateReferenceSystemId.Epsg(4480),
        "China Geodetic Coordinate System 2000 geographic 3D",
        GeodeticDatum.Cgcs2000);

    public CoordinateReferenceSystemId Id { get; }

    public string Name { get; }

    public GeodeticDatum Datum { get; }

    public GeodeticAxisOrder AxisOrder { get; }
}
