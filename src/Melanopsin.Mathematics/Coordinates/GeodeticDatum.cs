namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A geodetic datum identity together with its reference ellipsoid.
/// </summary>
/// <remarks>
/// A datum is more than an ellipsoid: it also fixes an Earth-fixed reference
/// frame, realization, and possibly a reference epoch. Two coordinate systems
/// that use the same ellipsoid are not necessarily on the same datum.
/// </remarks>
public sealed record GeodeticDatum {
    /// <summary>
    /// Initializes a geodetic datum.
    /// </summary>
    /// <param name="name">A human-readable datum name.</param>
    /// <param name="ellipsoid">The datum's reference ellipsoid.</param>
    /// <param name="referenceEpoch">
    /// An optional decimal year that identifies the datum reference epoch.
    /// </param>
    public GeodeticDatum(
        string name,
        Ellipsoid ellipsoid,
        double? referenceEpoch = null) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException(
                "A datum name is required.",
                nameof(name));
        }

        if (referenceEpoch is { } epoch && !double.IsFinite(epoch)) {
            throw new ArgumentOutOfRangeException(nameof(referenceEpoch));
        }

        Name = name;
        Ellipsoid = ellipsoid;
        ReferenceEpoch = referenceEpoch;
    }

    /// <summary>Gets the WGS84 geodetic datum.</summary>
    public static GeodeticDatum Wgs84 { get; } = new(
        "World Geodetic System 1984",
        Ellipsoid.Wgs84);

    /// <summary>Gets the China Geodetic Coordinate System 2000 datum.</summary>
    /// <remarks>
    /// The CGCS2000 reference epoch is 2000.0. This identity records the
    /// epoch but does not apply a time-dependent crustal-motion model.
    /// </remarks>
    public static GeodeticDatum Cgcs2000 { get; } = new(
        "China Geodetic Coordinate System 2000",
        Ellipsoid.Cgcs2000,
        2000.0);

    public string Name { get; }

    public Ellipsoid Ellipsoid { get; }

    /// <summary>Gets the reference epoch as a decimal year, when defined.</summary>
    public double? ReferenceEpoch { get; }
}
