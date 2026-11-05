// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A reference ellipsoid of revolution used by geodetic coordinate systems.
/// </summary>
/// <remarks>
/// The ellipsoid is defined by its semi-major axis and flattening. A flattening
/// of zero represents a sphere. Named presets describe only the figure of an
/// ellipsoid; they do not define a geodetic datum, an Earth-fixed frame,
/// coordinate epoch, or a transformation between datums. Two systems can share
/// the same ellipsoid while still requiring different datum transformations.
/// </remarks>
public readonly record struct Ellipsoid {
    public Ellipsoid(double semiMajorAxis, double flattening) {
        if (!double.IsFinite(semiMajorAxis) || semiMajorAxis <= 0) {
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis));
        }

        if (!double.IsFinite(flattening) || flattening < 0 || flattening >= 1) {
            throw new ArgumentOutOfRangeException(nameof(flattening));
        }

        SemiMajorAxis = semiMajorAxis;
        Flattening = flattening;
    }

    /// <summary>Gets the WGS84 reference ellipsoid.</summary>
    /// <remarks>
    /// Semi-major axis: 6,378,137 m; inverse flattening: 298.257223563
    /// (NIMA TR8350.2, 3rd ed., Amendment 1, 2000).
    /// </remarks>
    public static Ellipsoid Wgs84 => new(6_378_137, 1 / 298.257223563);

    /// <summary>Gets the China 2000 reference ellipsoid (CGCS2000).</summary>
    /// <remarks>
    /// Semi-major axis: 6,378,137 m; inverse flattening: 298.257222101.
    /// The ellipsoid is the one registered as EPSG ellipsoid 1024 and used by
    /// the China Geodetic Coordinate System 2000 geographic CRS (EPSG:4490).
    /// The CGCS2000 realization in GB 22021-2008 also specifies datum origin,
    /// orientation, and epoch; this preset supplies only the ellipsoid shape.
    /// Selecting it does not transform coordinates from WGS84 or another datum.
    /// </remarks>
    public static Ellipsoid Cgcs2000 => new(6_378_137, 1 / 298.257222101);

    /// <summary>Gets the Geodetic Reference System 1980 ellipsoid (GRS80).</summary>
    /// <remarks>
    /// Semi-major axis: 6,378,137 m; inverse flattening: 298.257222101.
    /// The defining parameters are those of the Geodetic Reference System 1980
    /// and correspond to EPSG ellipsoid 7019. GRS80 is used by several
    /// contemporary geodetic datums; this preset supplies only its figure and
    /// does not imply a particular datum or reference frame.
    /// </remarks>
    public static Ellipsoid Grs80 => new(6_378_137, 1 / 298.257222101);

    public double SemiMajorAxis { get; }

    public double Flattening { get; }

    public double SemiMinorAxis => SemiMajorAxis * (1 - Flattening);

    public double FirstEccentricitySquared => Flattening * (2 - Flattening);

    public double SecondEccentricitySquared {
        get {
            var first = FirstEccentricitySquared;
            return first / (1 - first);
        }
    }

    /// <summary>
    /// Gets whether this value describes a finite, non-degenerate ellipsoid of
    /// revolution with a polar semi-axis shorter than its equatorial semi-axis.
    /// </summary>
    public bool IsValid =>
        double.IsFinite(SemiMajorAxis) &&
        SemiMajorAxis > 0 &&
        double.IsFinite(Flattening) &&
        Flattening >= 0 &&
        Flattening < 1;
}
