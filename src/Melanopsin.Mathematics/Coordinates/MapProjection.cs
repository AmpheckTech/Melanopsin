// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A projected easting and northing in the linear unit of the source ellipsoid.
/// </summary>
/// <remarks>
/// The axes are explicit: easting is the projected coordinate parallel to the
/// projection's false-origin easting axis, and northing is the projected
/// coordinate parallel to its false-origin northing axis. Neither property
/// implies a raster row direction or a screen-space Y orientation.
/// </remarks>
public readonly record struct ProjectedCoordinate {
    public ProjectedCoordinate(double easting, double northing) {
        if (!double.IsFinite(easting) || !double.IsFinite(northing)) {
            throw new ArgumentOutOfRangeException(
                nameof(easting),
                "Projected coordinates must be finite.");
        }

        Easting = easting;
        Northing = northing;
    }

    /// <summary>Gets the easting in the projection's linear unit.</summary>
    public double Easting { get; }

    /// <summary>Gets the northing in the projection's linear unit.</summary>
    public double Northing { get; }

    /// <summary>Gets whether both projected components are finite.</summary>
    public bool IsFinite =>
        double.IsFinite(Easting) &&
        double.IsFinite(Northing);
}

/// <summary>
/// An ellipsoidal Mercator projection with an explicit natural origin.
/// </summary>
/// <remarks>
/// <para>
/// Longitude increases eastward and latitude increases northward. Angles are
/// supplied in degrees; projected coordinates use the semi-major-axis unit.
/// Ellipsoidal height is not used. The forward equations follow Snyder,
/// <i>Map Projections: A Working Manual</i>, USGS Professional Paper 1395
/// (1987), and the EPSG Guidance Note 7-2 parameter conventions.
/// </para>
/// <para>
/// The exact poles are outside the projection domain. No fixed absolute
/// tolerance is applied to latitude. Finite inputs that overflow an
/// intermediate result are reported as non-finite output.
/// </para>
/// </remarks>
public readonly record struct MercatorProjection {
    private readonly double _isometricOrigin;
    private readonly double _eccentricity;

    public MercatorProjection(
        Ellipsoid ellipsoid,
        double centralMeridianDegrees,
        double latitudeOfOriginDegrees = 0,
        double scaleFactor = 1,
        double falseEasting = 0,
        double falseNorthing = 0) {
        ValidateParameters(
            ellipsoid,
            centralMeridianDegrees,
            latitudeOfOriginDegrees,
            scaleFactor,
            falseEasting,
            falseNorthing);
        Ellipsoid = ellipsoid;
        CentralMeridianDegrees = centralMeridianDegrees;
        LatitudeOfOriginDegrees = latitudeOfOriginDegrees;
        ScaleFactor = scaleFactor;
        FalseEasting = falseEasting;
        FalseNorthing = falseNorthing;
        _eccentricity = Math.Sqrt(ellipsoid.FirstEccentricitySquared);
        _isometricOrigin = Isometric(
            latitudeOfOriginDegrees * (Math.PI / 180),
            _eccentricity);
    }

    /// <summary>Gets the source ellipsoid.</summary>
    public Ellipsoid Ellipsoid { get; }

    /// <summary>Gets the central-meridian longitude in degrees.</summary>
    public double CentralMeridianDegrees { get; }

    /// <summary>Gets the latitude of the natural origin in degrees.</summary>
    public double LatitudeOfOriginDegrees { get; }

    /// <summary>Gets the projection scale factor at the natural origin.</summary>
    public double ScaleFactor { get; }

    /// <summary>Gets the false easting.</summary>
    public double FalseEasting { get; }

    /// <summary>Gets the false northing.</summary>
    public double FalseNorthing { get; }

    /// <summary>Projects a geodetic coordinate.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The coordinate is at an exact pole.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The finite input produces a non-finite projected coordinate.
    /// </exception>
    public ProjectedCoordinate Forward(GeodeticCoordinate coordinate) {
        if (!TryForward(coordinate, out var result, out var report)) {
            if (report.Status == CoordinateOperationStatus.OutOfDomain) {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    "The exact poles are outside the Mercator projection domain.");
            }

            throw new ArithmeticException(
                "The Mercator projection produced a non-finite result.");
        }

        return result;
    }

    public bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result) {
        return TryForward(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts a forward projection and distinguishes an out-of-domain pole
    /// from a non-finite intermediate result.
    /// </summary>
    public bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!MapProjectionMath.IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var latitude = MapProjectionMath.DegreesToRadians(coordinate.LatitudeDegrees);
        if (Math.Abs(coordinate.LatitudeDegrees) == 90) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.OutOfDomain);
            return false;
        }

        var longitudeDifference = MapProjectionMath.NormalizeLongitudeRadians(
            MapProjectionMath.DegreesToRadians(
                coordinate.LongitudeDegrees - CentralMeridianDegrees));
        var isometric = Isometric(latitude, _eccentricity);
        var easting = FalseEasting +
            (Ellipsoid.SemiMajorAxis * ScaleFactor * longitudeDifference);
        var northing = FalseNorthing +
            (Ellipsoid.SemiMajorAxis * ScaleFactor *
                (isometric - _isometricOrigin));
        if (!double.IsFinite(easting) || !double.IsFinite(northing)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new ProjectedCoordinate(easting, northing);
        report = new CoordinateOperationReport(
            CoordinateOperation.MercatorForward,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Unprojects an easting and northing.</summary>
    /// <exception cref="ArithmeticException">
    /// The inverse iteration does not converge or produces a non-finite result.
    /// </exception>
    public GeodeticCoordinate Inverse(ProjectedCoordinate coordinate) {
        if (!TryInverse(coordinate, out var result, out var report)) {
            throw new ArithmeticException(
                report.Status == CoordinateOperationStatus.NonConverged
                    ? "The inverse Mercator iteration did not converge."
                    : "The inverse Mercator projection produced an invalid result.");
        }

        return result;
    }

    public bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result) {
        return TryInverse(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts an inverse projection and reports non-finite intermediates or
    /// failure to solve the conformal-latitude equation.
    /// </summary>
    public bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!coordinate.IsFinite) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var longitudeDifference =
            (coordinate.Easting - FalseEasting) /
            (Ellipsoid.SemiMajorAxis * ScaleFactor);
        var isometric =
            ((coordinate.Northing - FalseNorthing) /
                (Ellipsoid.SemiMajorAxis * ScaleFactor)) +
            _isometricOrigin;
        if (!double.IsFinite(isometric)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        var conformalTangent = Math.Exp(isometric);
        if (!double.IsFinite(conformalTangent)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        var latitude = (Math.PI / 2) -
            (2 * Math.Atan(conformalTangent));
        var converged = false;
        for (var iteration = 0; iteration < 20; iteration++) {
            var sineLatitude = Math.Sin(latitude);
            var ratio =
                (1 - (_eccentricity * sineLatitude)) /
                (1 + (_eccentricity * sineLatitude));
            var next = (Math.PI / 2) -
                (2 * Math.Atan(
                    conformalTangent * Math.Pow(ratio, _eccentricity / 2)));
            if (!double.IsFinite(next)) {
                break;
            }

            if (Math.Abs(next - latitude) <= 1e-14) {
                latitude = next;
                converged = true;
                break;
            }

            latitude = next;
        }

        if (!converged) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonConverged);
            return false;
        }

        var longitude = MapProjectionMath.NormalizeLongitudeRadians(
            MapProjectionMath.DegreesToRadians(CentralMeridianDegrees) +
            longitudeDifference);
        var latitudeDegrees = MapProjectionMath.RadiansToDegrees(latitude);
        var longitudeDegrees = MapProjectionMath.RadiansToDegrees(longitude);
        if (!double.IsFinite(latitudeDegrees) ||
            !double.IsFinite(longitudeDegrees)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new GeodeticCoordinate(
            latitudeDegrees,
            longitudeDegrees,
            height: 0);
        report = new CoordinateOperationReport(
            CoordinateOperation.MercatorInverse,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    private static double Isometric(double latitude, double eccentricity) {
        var sineLatitude = Math.Sin(latitude);
        return Math.Log(
            Math.Tan((Math.PI / 4) + (latitude / 2)) *
            Math.Pow(
                (1 - (eccentricity * sineLatitude)) /
                    (1 + (eccentricity * sineLatitude)),
                eccentricity / 2));
    }

    private static void ValidateParameters(
        Ellipsoid ellipsoid,
        double centralMeridianDegrees,
        double latitudeOfOriginDegrees,
        double scaleFactor,
        double falseEasting,
        double falseNorthing) {
        if (!ellipsoid.IsValid) {
            throw new ArgumentOutOfRangeException(nameof(ellipsoid));
        }

        if (!double.IsFinite(centralMeridianDegrees) ||
            centralMeridianDegrees < -180 ||
            centralMeridianDegrees > 180) {
            throw new ArgumentOutOfRangeException(
                nameof(centralMeridianDegrees));
        }

        if (!double.IsFinite(latitudeOfOriginDegrees) ||
            latitudeOfOriginDegrees <= -90 ||
            latitudeOfOriginDegrees >= 90) {
            throw new ArgumentOutOfRangeException(
                nameof(latitudeOfOriginDegrees));
        }

        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0) {
            throw new ArgumentOutOfRangeException(nameof(scaleFactor));
        }

        if (!double.IsFinite(falseEasting)) {
            throw new ArgumentOutOfRangeException(nameof(falseEasting));
        }

        if (!double.IsFinite(falseNorthing)) {
            throw new ArgumentOutOfRangeException(nameof(falseNorthing));
        }
    }

}

/// <summary>
/// An ellipsoidal transverse Mercator projection with an explicit natural
/// origin.
/// </summary>
/// <remarks>
/// <para>
/// The implementation uses the standard sixth-order Snyder/EPSG series. It is
/// intended for conventional narrow-zone use; accuracy degrades far from the
/// central meridian even though the formulas remain finite. No hidden
/// proximity threshold is applied. Angles are in degrees and projected values
/// use the ellipsoid's semi-major-axis unit. Height is not used.
/// </para>
/// <para>
/// Sources: Snyder, USGS Professional Paper 1395 (1987), and the EPSG Guidance
/// Note 7-2 formulas for Transverse Mercator. The conformal series and sign
/// conventions correspond to a right-handed geographic coordinate system with
/// longitude increasing eastward and latitude increasing northward.
/// </para>
/// </remarks>
public readonly record struct TransverseMercatorProjection {
    private readonly double _eccentricity;
    private readonly double _eccentricitySquared;
    private readonly double _secondEccentricitySquared;
    private readonly double _originMeridionalArc;

    public TransverseMercatorProjection(
        Ellipsoid ellipsoid,
        double centralMeridianDegrees,
        double latitudeOfOriginDegrees = 0,
        double scaleFactor = 1,
        double falseEasting = 0,
        double falseNorthing = 0) {
        ValidateParameters(
            ellipsoid,
            centralMeridianDegrees,
            latitudeOfOriginDegrees,
            scaleFactor,
            falseEasting,
            falseNorthing);
        Ellipsoid = ellipsoid;
        CentralMeridianDegrees = centralMeridianDegrees;
        LatitudeOfOriginDegrees = latitudeOfOriginDegrees;
        ScaleFactor = scaleFactor;
        FalseEasting = falseEasting;
        FalseNorthing = falseNorthing;
        _eccentricitySquared = ellipsoid.FirstEccentricitySquared;
        _eccentricity = Math.Sqrt(_eccentricitySquared);
        _secondEccentricitySquared = ellipsoid.SecondEccentricitySquared;
        _originMeridionalArc = MeridionalArc(
            ellipsoid,
            MapProjectionMath.DegreesToRadians(latitudeOfOriginDegrees));
    }

    /// <summary>Gets the source ellipsoid.</summary>
    public Ellipsoid Ellipsoid { get; }

    /// <summary>Gets the central-meridian longitude in degrees.</summary>
    public double CentralMeridianDegrees { get; }

    /// <summary>Gets the latitude of the natural origin in degrees.</summary>
    public double LatitudeOfOriginDegrees { get; }

    /// <summary>Gets the central scale factor.</summary>
    public double ScaleFactor { get; }

    /// <summary>Gets the false easting.</summary>
    public double FalseEasting { get; }

    /// <summary>Gets the false northing.</summary>
    public double FalseNorthing { get; }

    /// <summary>
    /// Creates the common CGCS2000 Gauss-Krüger convention with an explicit
    /// zone width and false-easting prefix choice.
    /// </summary>
    /// <param name="zoneNumber">The province/engineering zone number.</param>
    /// <param name="zoneWidthDegrees">Either 3 or 6 degrees.</param>
    /// <param name="prependZoneNumber">
    /// Whether to add <c>zoneNumber * 1,000,000</c> to the 500,000 m false
    /// easting. The choice is explicit because both conventions are in use.
    /// </param>
    /// <remarks>
    /// The central meridian is <c>3 * zoneNumber</c> for a 3-degree zone and
    /// <c>6 * zoneNumber - 3</c> for a 6-degree zone. The ellipsoid is
    /// CGCS2000, the origin latitude is zero, and the scale factor is one.
    /// Zone numbering here follows the conventional zero-meridian-based
    /// Chinese engineering numbering; it is not inferred from longitude.
    /// </remarks>
    public static TransverseMercatorProjection CreateCgcs2000GaussKruger(
        int zoneNumber,
        int zoneWidthDegrees,
        bool prependZoneNumber = false) {
        if (zoneWidthDegrees is not (3 or 6)) {
            throw new ArgumentOutOfRangeException(nameof(zoneWidthDegrees));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneNumber);

        var centralMeridian = zoneWidthDegrees == 3
            ? 3.0 * zoneNumber
            : (6.0 * zoneNumber) - 3;
        if (centralMeridian > 180) {
            throw new ArgumentOutOfRangeException(nameof(zoneNumber));
        }

        var falseEasting = 500_000.0 +
            (prependZoneNumber ? zoneNumber * 1_000_000.0 : 0);
        return new TransverseMercatorProjection(
            Ellipsoid.Cgcs2000,
            centralMeridian,
            latitudeOfOriginDegrees: 0,
            scaleFactor: 1,
            falseEasting,
            falseNorthing: 0);
    }

    /// <summary>Projects a geodetic coordinate.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The coordinate is at an exact pole.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The finite input produces a non-finite projected coordinate.
    /// </exception>
    public ProjectedCoordinate Forward(GeodeticCoordinate coordinate) {
        if (!TryForward(coordinate, out var result, out var report)) {
            if (report.Status == CoordinateOperationStatus.OutOfDomain) {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    "The exact poles are outside the transverse Mercator domain.");
            }

            throw new ArithmeticException(
                "The transverse Mercator projection produced an invalid result.");
        }

        return result;
    }

    public bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result) {
        return TryForward(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts a forward transverse Mercator projection and reports a pole or
    /// a non-finite intermediate result.
    /// </summary>
    public bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!MapProjectionMath.IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.TransverseMercatorForward,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (Math.Abs(coordinate.LatitudeDegrees) == 90) {
            report = new CoordinateOperationReport(
                CoordinateOperation.TransverseMercatorForward,
                CoordinateOperationStatus.OutOfDomain);
            return false;
        }

        var latitude = MapProjectionMath.DegreesToRadians(coordinate.LatitudeDegrees);
        var longitudeDifference = MapProjectionMath.NormalizeLongitudeRadians(
            MapProjectionMath.DegreesToRadians(
                coordinate.LongitudeDegrees - CentralMeridianDegrees));
        var sineLatitude = Math.Sin(latitude);
        var cosineLatitude = Math.Cos(latitude);
        var tangent = Math.Tan(latitude);
        var tangentSquared = tangent * tangent;
        var curvature = _eccentricitySquared /
            (1 - _eccentricitySquared);
        var c = curvature * cosineLatitude * cosineLatitude;
        var a = longitudeDifference * cosineLatitude;
        var aSquared = a * a;
        var primeVerticalRadius = Ellipsoid.SemiMajorAxis /
            Math.Sqrt(
                1 - (_eccentricitySquared * sineLatitude * sineLatitude));
        var meridionalArc = MeridionalArc(Ellipsoid, latitude);
        var easting = FalseEasting + (ScaleFactor * primeVerticalRadius *
            (a + (((1 - tangentSquared + c) * a * aSquared) / 6) +
                (((5 - (18 * tangentSquared) + tangentSquared * tangentSquared +
                    (72 * c) - (58 * curvature)) * a * aSquared * aSquared) /
                    120)));
        var northing = FalseNorthing +
            (ScaleFactor * ((meridionalArc - _originMeridionalArc) +
                (primeVerticalRadius * tangent *
                    ((aSquared / 2) +
                        (((5 - tangentSquared + (9 * c) + (4 * c * c)) *
                            aSquared * aSquared) /
                            24) +
                        (((61 - (58 * tangentSquared) +
                            (tangentSquared * tangentSquared) + (600 * c) -
                            (330 * curvature)) * aSquared * aSquared * aSquared) /
                            720)))));
        if (!double.IsFinite(easting) || !double.IsFinite(northing)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.TransverseMercatorForward,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new ProjectedCoordinate(easting, northing);
        report = new CoordinateOperationReport(
            CoordinateOperation.TransverseMercatorForward,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Unprojects an easting and northing.</summary>
    /// <exception cref="ArithmeticException">
    /// The finite input produces a non-finite geodetic coordinate.
    /// </exception>
    public GeodeticCoordinate Inverse(ProjectedCoordinate coordinate) {
        if (!TryInverse(coordinate, out var result, out var report)) {
            throw new ArithmeticException(
                "The inverse transverse Mercator projection produced an invalid result.");
        }

        return result;
    }

    public bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result) {
        return TryInverse(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts an inverse transverse Mercator projection and reports a
    /// non-finite intermediate or unrepresentable geodetic coordinate.
    /// </summary>
    public bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!coordinate.IsFinite) {
            report = new CoordinateOperationReport(
                CoordinateOperation.TransverseMercatorInverse,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var meridionalArc = _originMeridionalArc +
            ((coordinate.Northing - FalseNorthing) / ScaleFactor);
        var rectifyingRadius = Ellipsoid.SemiMajorAxis *
            (1 - (_eccentricitySquared / 4) -
                ((3 * _eccentricitySquared * _eccentricitySquared) / 64) -
                ((5 * _eccentricitySquared *
                    _eccentricitySquared * _eccentricitySquared) / 256));
        var mu = meridionalArc / rectifyingRadius;
        var e1 = (1 - Math.Sqrt(1 - _eccentricitySquared)) /
            (1 + Math.Sqrt(1 - _eccentricitySquared));
        var e1Squared = e1 * e1;
        var e1Cubed = e1Squared * e1;
        var e1Fourth = e1Squared * e1Squared;
        var footprint = mu +
            (((3 * e1 / 2) - ((27 * e1Cubed) / 32)) * Math.Sin(2 * mu)) +
            (((21 * e1Squared / 16) - ((55 * e1Fourth) / 32)) *
                Math.Sin(4 * mu)) +
            ((151 * e1Cubed / 96) * Math.Sin(6 * mu)) +
            ((1097 * e1Fourth / 512) * Math.Sin(8 * mu));
        var sineFootprint = Math.Sin(footprint);
        var cosineFootprint = Math.Cos(footprint);
        var tangentFootprint = Math.Tan(footprint);
        var c = _secondEccentricitySquared *
            cosineFootprint * cosineFootprint;
        var t = tangentFootprint * tangentFootprint;
        var primeVerticalRadius = Ellipsoid.SemiMajorAxis /
            Math.Sqrt(
                1 - (_eccentricitySquared * sineFootprint * sineFootprint));
        var meridionalRadius = Ellipsoid.SemiMajorAxis *
            (1 - _eccentricitySquared) /
            Math.Pow(
                1 - (_eccentricitySquared * sineFootprint * sineFootprint),
                1.5);
        var d = (coordinate.Easting - FalseEasting) /
            (primeVerticalRadius * ScaleFactor);
        var dSquared = d * d;
        var latitude = footprint -
            (((primeVerticalRadius * tangentFootprint) / meridionalRadius) *
                ((dSquared / 2) -
                    (((5 + (3 * t) + (10 * c) - (4 * c * c) -
                        (9 * _secondEccentricitySquared)) *
                        dSquared * dSquared) /
                        24) +
                    (((61 + (90 * t) + (298 * c) + (45 * t * t) -
                        (252 * _secondEccentricitySquared) - (3 * c * c)) *
                        dSquared * dSquared * dSquared) /
                        720)));
        var longitudeDifference =
            (d -
                (((1 + (2 * t) + c) * d * dSquared) / 6) +
                (((5 - (2 * c) + (28 * t) - (3 * c * c) +
                    (8 * _secondEccentricitySquared) + (24 * t * t)) *
                    d * dSquared * dSquared) /
                    120)) /
            cosineFootprint;
        var longitude = MapProjectionMath.NormalizeLongitudeRadians(
            MapProjectionMath.DegreesToRadians(CentralMeridianDegrees) +
            longitudeDifference);
        var latitudeDegrees = MapProjectionMath.RadiansToDegrees(latitude);
        var longitudeDegrees = MapProjectionMath.RadiansToDegrees(longitude);
        if (!double.IsFinite(latitudeDegrees) ||
            !double.IsFinite(longitudeDegrees) ||
            latitudeDegrees < -90 ||
            latitudeDegrees > 90) {
            report = new CoordinateOperationReport(
                CoordinateOperation.TransverseMercatorInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new GeodeticCoordinate(
            latitudeDegrees,
            longitudeDegrees,
            height: 0);
        report = new CoordinateOperationReport(
            CoordinateOperation.TransverseMercatorInverse,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    private static double MeridionalArc(Ellipsoid ellipsoid, double latitude) {
        var e2 = ellipsoid.FirstEccentricitySquared;
        var e4 = e2 * e2;
        var e6 = e4 * e2;
        return ellipsoid.SemiMajorAxis *
            ((1 - (e2 / 4) - ((3 * e4) / 64) - ((5 * e6) / 256)) * latitude -
                (((3 * e2 / 8) + ((3 * e4) / 32) + ((45 * e6) / 1024)) *
                    Math.Sin(2 * latitude)) +
                (((15 * e4 / 256) + ((45 * e6) / 1024)) *
                    Math.Sin(4 * latitude)) -
                ((35 * e6 / 3072) * Math.Sin(6 * latitude)));
    }

    private static void ValidateParameters(
        Ellipsoid ellipsoid,
        double centralMeridianDegrees,
        double latitudeOfOriginDegrees,
        double scaleFactor,
        double falseEasting,
        double falseNorthing) {
        if (!ellipsoid.IsValid) {
            throw new ArgumentOutOfRangeException(nameof(ellipsoid));
        }

        if (!double.IsFinite(centralMeridianDegrees) ||
            centralMeridianDegrees < -180 ||
            centralMeridianDegrees > 180) {
            throw new ArgumentOutOfRangeException(
                nameof(centralMeridianDegrees));
        }

        if (!double.IsFinite(latitudeOfOriginDegrees) ||
            latitudeOfOriginDegrees <= -90 ||
            latitudeOfOriginDegrees >= 90) {
            throw new ArgumentOutOfRangeException(
                nameof(latitudeOfOriginDegrees));
        }

        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0) {
            throw new ArgumentOutOfRangeException(nameof(scaleFactor));
        }

        if (!double.IsFinite(falseEasting)) {
            throw new ArgumentOutOfRangeException(nameof(falseEasting));
        }

        if (!double.IsFinite(falseNorthing)) {
            throw new ArgumentOutOfRangeException(nameof(falseNorthing));
        }
    }

}

/// <summary>
/// EPSG:3857 and EPSG:900913 Web Mercator helpers.
/// </summary>
/// <remarks>
/// Web Mercator uses the WGS84 semi-major axis as a sphere and ignores
/// ellipsoidal flattening. EPSG Guidance Note 7-2 and the EPSG Registry define
/// the conventional latitude limits at approximately ±85.0511287798066
/// degrees. The constants and infinite-pole formula are documented here
/// instead of being hidden in a global raster or map convention.
/// </remarks>
public static class WebMercator {
    private const double SemiMajorAxis = 6_378_137;

    /// <summary>Gets the conventional maximum latitude in degrees.</summary>
    public const double MaximumLatitudeDegrees = 85.0511287798066;

    /// <summary>Projects a geodetic coordinate to EPSG:3857.</summary>
    public static ProjectedCoordinate Forward(
        GeodeticCoordinate coordinate) {
        if (!TryForward(coordinate, out var result, out var report)) {
            if (report.Status == CoordinateOperationStatus.OutOfDomain) {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    "The latitude is outside the conventional Web Mercator domain.");
            }

            throw new ArithmeticException(
                "The Web Mercator projection produced an invalid result.");
        }

        return result;
    }

    public static bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result) {
        return TryForward(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts a Web Mercator projection and reports latitudes outside the
    /// conventional EPSG:3857 area of use.
    /// </summary>
    public static bool TryForward(
        GeodeticCoordinate coordinate,
        out ProjectedCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!MapProjectionMath.IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (Math.Abs(coordinate.LatitudeDegrees) > MaximumLatitudeDegrees) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.OutOfDomain);
            return false;
        }

        var latitude = MapProjectionMath.DegreesToRadians(coordinate.LatitudeDegrees);
        var longitude = MapProjectionMath.DegreesToRadians(coordinate.LongitudeDegrees);
        var easting = SemiMajorAxis * longitude;
        var northing = SemiMajorAxis *
            Math.Log(Math.Tan((Math.PI / 4) + (latitude / 2)));
        if (!double.IsFinite(easting) || !double.IsFinite(northing)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorForward,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new ProjectedCoordinate(easting, northing);
        report = new CoordinateOperationReport(
            CoordinateOperation.MercatorForward,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Unprojects an EPSG:3857 coordinate.</summary>
    public static GeodeticCoordinate Inverse(
        ProjectedCoordinate coordinate) {
        if (!TryInverse(coordinate, out var result, out var report)) {
            throw new ArithmeticException(
                "The inverse Web Mercator projection produced an invalid result.");
        }

        return result;
    }

    public static bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result) {
        return TryInverse(coordinate, out result, out _);
    }

    /// <summary>
    /// Attempts an inverse Web Mercator projection and reports non-finite or
    /// unrepresentable results.
    /// </summary>
    public static bool TryInverse(
        ProjectedCoordinate coordinate,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!coordinate.IsFinite) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var longitude = coordinate.Easting / SemiMajorAxis;
        var latitude = (Math.PI / 2) -
            (2 * Math.Atan(Math.Exp(-coordinate.Northing / SemiMajorAxis)));
        var longitudeDegrees = MapProjectionMath.RadiansToDegrees(
            MapProjectionMath.NormalizeLongitudeRadians(longitude));
        var latitudeDegrees = MapProjectionMath.RadiansToDegrees(latitude);
        if (!double.IsFinite(latitudeDegrees) ||
            !double.IsFinite(longitudeDegrees) ||
            Math.Abs(latitudeDegrees) > 90) {
            report = new CoordinateOperationReport(
                CoordinateOperation.MercatorInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new GeodeticCoordinate(
            latitudeDegrees,
            longitudeDegrees,
            height: 0);
        report = new CoordinateOperationReport(
            CoordinateOperation.MercatorInverse,
            CoordinateOperationStatus.Succeeded);
        return true;
    }
}

internal static class MapProjectionMath {
    public static double DegreesToRadians(double degrees) {
        return degrees * (Math.PI / 180);
    }

    public static double RadiansToDegrees(double radians) {
        return radians * (180 / Math.PI);
    }

    public static double NormalizeLongitudeRadians(double radians) {
        const double twoPi = 2 * Math.PI;
        radians %= twoPi;
        if (radians < -Math.PI) {
            radians += twoPi;
        } else if (radians > Math.PI) {
            radians -= twoPi;
        }

        return radians;
    }

    public static bool IsFinite(GeodeticCoordinate coordinate) {
        return double.IsFinite(coordinate.LatitudeDegrees) &&
            double.IsFinite(coordinate.LongitudeDegrees) &&
            double.IsFinite(coordinate.Height);
    }
}
