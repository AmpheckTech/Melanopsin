// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Ellipsoid-aware geodetic coordinate conversions and geodesic calculations.
/// </summary>
/// <remarks>
/// All angular inputs and outputs are in degrees. ECEF and ENU distances use
/// the units of the ellipsoid's semi-major axis. The default ellipsoid is
/// WGS84. This type computes coordinate relationships only; it does not model
/// datums, map projections, tiles, layers, or geographic documents. Passing a
/// named ellipsoid such as <see cref="Ellipsoid.Cgcs2000"/> selects its figure
/// for the calculation; it does not perform a datum transformation or account
/// for a coordinate epoch. WGS84 defining parameters follow NIMA TR8350.2,
/// 3rd ed., Amendment 1 (2000).
/// Vincenty solutions follow Vincenty (1975),
/// <see href="https://doi.org/10.1179/sre.1975.23.176.88">doi:10.1179/sre.1975.23.176.88</see>.
/// </remarks>
public static class Geodesy {
    private const double DegreesToRadians = Math.PI / 180;
    private const double RadiansToDegrees = 180 / Math.PI;
    private const double VincentyTolerance = 1e-12;
    private const int VincentyMaximumIterations = 200;

    public static EcefCoordinate ToEcef(GeodeticCoordinate coordinate) {
        return ToEcef(coordinate, Ellipsoid.Wgs84);
    }

    /// <summary>
    /// Converts geodetic coordinates to Earth-fixed Cartesian coordinates using
    /// WGS84 and reports non-finite input or output.
    /// </summary>
    public static bool TryToEcef(
        GeodeticCoordinate coordinate,
        out EcefCoordinate result) {
        return TryToEcef(
            coordinate,
            Ellipsoid.Wgs84,
            out result,
            out _);
    }

    /// <summary>
    /// Converts geodetic coordinates to Earth-fixed Cartesian coordinates and
    /// reports invalid ellipsoid data or non-finite results.
    /// </summary>
    public static bool TryToEcef(
        GeodeticCoordinate coordinate,
        Ellipsoid ellipsoid,
        out EcefCoordinate result) {
        return TryToEcef(coordinate, ellipsoid, out result, out _);
    }

    /// <summary>
    /// Converts geodetic coordinates to Earth-fixed Cartesian coordinates and
    /// reports the terminating condition.
    /// </summary>
    public static EcefCoordinate ToEcef(
        GeodeticCoordinate coordinate,
        Ellipsoid ellipsoid) {
        if (!TryToEcef(coordinate, ellipsoid, out var result, out var report)) {
            ThrowForFailure(report);
        }

        return result;
    }

    /// <summary>
    /// Converts geodetic coordinates to Earth-fixed Cartesian coordinates and
    /// reports the terminating condition.
    /// </summary>
    public static bool TryToEcef(
        GeodeticCoordinate coordinate,
        Ellipsoid ellipsoid,
        out EcefCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticToEcef,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!ellipsoid.IsValid) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticToEcef,
                CoordinateOperationStatus.InvalidInput);
            return false;
        }

        var latitude = coordinate.LatitudeDegrees * DegreesToRadians;
        var longitude = coordinate.LongitudeDegrees * DegreesToRadians;
        var sineLatitude = Math.Sin(latitude);
        var cosineLatitude = Math.Cos(latitude);
        var eccentricitySquared = ellipsoid.FirstEccentricitySquared;
        var primeVerticalRadius = ellipsoid.SemiMajorAxis /
            Math.Sqrt(
                1 - (eccentricitySquared * sineLatitude * sineLatitude));
        var horizontal = (primeVerticalRadius + coordinate.Height) * cosineLatitude;
        var x = horizontal * Math.Cos(longitude);
        var y = horizontal * Math.Sin(longitude);
        var z = ((primeVerticalRadius * (1 - eccentricitySquared)) +
            coordinate.Height) * sineLatitude;
        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticToEcef,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new EcefCoordinate(x, y, z);
        report = new CoordinateOperationReport(
            CoordinateOperation.GeodeticToEcef,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    public static GeodeticCoordinate ToGeodetic(EcefCoordinate coordinate) {
        return ToGeodetic(coordinate, Ellipsoid.Wgs84);
    }

    /// <summary>
    /// Converts Earth-fixed Cartesian coordinates to geodetic coordinates using
    /// WGS84 and reports non-finite or non-converged results.
    /// </summary>
    public static bool TryToGeodetic(
        EcefCoordinate coordinate,
        out GeodeticCoordinate result) {
        return TryToGeodetic(
            coordinate,
            Ellipsoid.Wgs84,
            out result,
            out _);
    }

    /// <summary>
    /// Converts Earth-fixed Cartesian coordinates to geodetic coordinates and
    /// reports invalid ellipsoid data or numerical failure.
    /// </summary>
    public static bool TryToGeodetic(
        EcefCoordinate coordinate,
        Ellipsoid ellipsoid,
        out GeodeticCoordinate result) {
        return TryToGeodetic(coordinate, ellipsoid, out result, out _);
    }

    /// <summary>
    /// Converts an ECEF coordinate to geodetic coordinates with an iterative
    /// latitude solution.
    /// </summary>
    public static GeodeticCoordinate ToGeodetic(
        EcefCoordinate coordinate,
        Ellipsoid ellipsoid) {
        if (!TryToGeodetic(coordinate, ellipsoid, out var result, out var report)) {
            ThrowForFailure(report);
        }

        return result;
    }

    /// <summary>
    /// Converts an ECEF coordinate to geodetic coordinates and reports invalid
    /// input, non-convergence, or non-finite output.
    /// </summary>
    /// <remarks>
    /// The latitude iteration stops when successive values differ by at most
    /// 1e-14 radians. Exhausting the iteration budget is a numerical failure
    /// reported as <see cref="CoordinateOperationStatus.NonConverged"/>; it is
    /// not treated as a successful approximation.
    /// </remarks>
    public static bool TryToGeodetic(
        EcefCoordinate coordinate,
        Ellipsoid ellipsoid,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        result = default;
        if (!IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.EcefToGeodetic,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!ellipsoid.IsValid) {
            report = new CoordinateOperationReport(
                CoordinateOperation.EcefToGeodetic,
                CoordinateOperationStatus.InvalidInput);
            return false;
        }

        var horizontal = FloatingPointMath.Hypot(coordinate.X, coordinate.Y);
        var longitude = horizontal == 0
            ? 0
            : Math.Atan2(coordinate.Y, coordinate.X);
        if (horizontal == 0) {
            var poleLatitude = coordinate.Z >= 0 ? 90.0 : -90.0;
            var poleHeight = Math.Abs(coordinate.Z) - ellipsoid.SemiMinorAxis;
            if (!double.IsFinite(poleHeight)) {
                report = new CoordinateOperationReport(
                    CoordinateOperation.EcefToGeodetic,
                    CoordinateOperationStatus.NonFiniteOutput);
                return false;
            }

            result = new GeodeticCoordinate(poleLatitude, 0, poleHeight);
            report = new CoordinateOperationReport(
                CoordinateOperation.EcefToGeodetic,
                CoordinateOperationStatus.Succeeded);
            return true;
        }

        var eccentricitySquared = ellipsoid.FirstEccentricitySquared;
        var latitude = Math.Atan2(
            coordinate.Z,
            horizontal * (1 - eccentricitySquared));
        var height = 0.0;
        var converged = false;
        for (var iteration = 0; iteration < 20; iteration++) {
            var sineLatitude = Math.Sin(latitude);
            var primeVerticalRadius = ellipsoid.SemiMajorAxis /
                Math.Sqrt(
                    1 - (eccentricitySquared * sineLatitude * sineLatitude));
            var cosineLatitude = Math.Cos(latitude);
            height = cosineLatitude == 0
                ? Math.Abs(coordinate.Z) - ellipsoid.SemiMinorAxis
                : (horizontal / cosineLatitude) - primeVerticalRadius;
            var nextLatitude = Math.Atan2(
                coordinate.Z + (eccentricitySquared * primeVerticalRadius * sineLatitude),
                horizontal);
            if (Math.Abs(nextLatitude - latitude) <= 1e-14) {
                latitude = nextLatitude;
                converged = true;
                break;
            }

            latitude = nextLatitude;
        }

        if (!converged) {
            report = new CoordinateOperationReport(
                CoordinateOperation.EcefToGeodetic,
                CoordinateOperationStatus.NonConverged);
            return false;
        }

        var latitudeDegrees = latitude * RadiansToDegrees;
        var longitudeDegrees = longitude * RadiansToDegrees;
        if (!double.IsFinite(latitudeDegrees) ||
            !double.IsFinite(longitudeDegrees) ||
            !double.IsFinite(height)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.EcefToGeodetic,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new GeodeticCoordinate(
            latitudeDegrees,
            longitudeDegrees,
            height);
        report = new CoordinateOperationReport(
            CoordinateOperation.EcefToGeodetic,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    public static EnuCoordinate ToEnu(
        GeodeticCoordinate reference,
        EcefCoordinate target) {
        return ToEnu(reference, target, Ellipsoid.Wgs84);
    }

    /// <summary>
    /// Expresses an ECEF position in the local East-North-Up frame at a
    /// geodetic reference point.
    /// </summary>
    public static EnuCoordinate ToEnu(
        GeodeticCoordinate reference,
        EcefCoordinate target,
        Ellipsoid ellipsoid) {
        var origin = ToEcef(reference, ellipsoid);
        var deltaX = target.X - origin.X;
        var deltaY = target.Y - origin.Y;
        var deltaZ = target.Z - origin.Z;
        var latitude = reference.LatitudeDegrees * DegreesToRadians;
        var longitude = reference.LongitudeDegrees * DegreesToRadians;
        var sineLatitude = Math.Sin(latitude);
        var cosineLatitude = Math.Cos(latitude);
        var sineLongitude = Math.Sin(longitude);
        var cosineLongitude = Math.Cos(longitude);
        var east = (-sineLongitude * deltaX) + (cosineLongitude * deltaY);
        var north =
            (-sineLatitude * cosineLongitude * deltaX) -
            (sineLatitude * sineLongitude * deltaY) +
            (cosineLatitude * deltaZ);
        var up =
            (cosineLatitude * cosineLongitude * deltaX) +
            (cosineLatitude * sineLongitude * deltaY) +
            (sineLatitude * deltaZ);
        return new EnuCoordinate(east, north, up);
    }

    /// <summary>
    /// Converts an ECEF coordinate to a local north-east-down frame at a
    /// geodetic reference point on an ellipsoid.
    /// </summary>
    public static NedCoordinate ToNed(
        EcefCoordinate coordinate,
        GeodeticCoordinate reference,
        Ellipsoid ellipsoid) {
        return NedCoordinate.FromEnu(ToEnu(reference, coordinate, ellipsoid));
    }

    /// <summary>
    /// Converts an ECEF coordinate to a local north-east-down frame using
    /// WGS84.
    /// </summary>
    public static NedCoordinate ToNed(
        EcefCoordinate coordinate,
        GeodeticCoordinate reference) {
        return ToNed(coordinate, reference, Ellipsoid.Wgs84);
    }

    /// <summary>
    /// Converts a local north-east-down coordinate to ECEF on an ellipsoid.
    /// </summary>
    public static EcefCoordinate FromNed(
        NedCoordinate coordinate,
        GeodeticCoordinate reference,
        Ellipsoid ellipsoid) {
        return FromEnu(reference, coordinate.ToEnu(), ellipsoid);
    }

    /// <summary>
    /// Converts a local north-east-down coordinate to ECEF using WGS84.
    /// </summary>
    public static EcefCoordinate FromNed(
        NedCoordinate coordinate,
        GeodeticCoordinate reference) {
        return FromNed(coordinate, reference, Ellipsoid.Wgs84);
    }

    public static EcefCoordinate FromEnu(
        GeodeticCoordinate reference,
        EnuCoordinate local) {
        return FromEnu(reference, local, Ellipsoid.Wgs84);
    }

    /// <summary>
    /// Converts a local East-North-Up offset back to ECEF.
    /// </summary>
    public static EcefCoordinate FromEnu(
        GeodeticCoordinate reference,
        EnuCoordinate local,
        Ellipsoid ellipsoid) {
        var origin = ToEcef(reference, ellipsoid);
        var latitude = reference.LatitudeDegrees * DegreesToRadians;
        var longitude = reference.LongitudeDegrees * DegreesToRadians;
        var sineLatitude = Math.Sin(latitude);
        var cosineLatitude = Math.Cos(latitude);
        var sineLongitude = Math.Sin(longitude);
        var cosineLongitude = Math.Cos(longitude);
        var east = local.East;
        var north = local.North;
        var up = local.Up;
        return new EcefCoordinate(
            origin.X + (-sineLongitude * east) -
                (sineLatitude * cosineLongitude * north) +
                (cosineLatitude * cosineLongitude * up),
            origin.Y + (cosineLongitude * east) -
                (sineLatitude * sineLongitude * north) +
                (cosineLatitude * sineLongitude * up),
            origin.Z + (cosineLatitude * north) + (sineLatitude * up));
    }

    /// <summary>
    /// Computes great-circle distance on a sphere using the haversine formula.
    /// </summary>
    public static double HaversineDistance(
        GeodeticCoordinate first,
        GeodeticCoordinate second,
        double sphereRadius = 6_371_008.8) {
        if (!double.IsFinite(sphereRadius) || sphereRadius <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sphereRadius));
        }

        var firstLatitude = first.LatitudeDegrees * DegreesToRadians;
        var secondLatitude = second.LatitudeDegrees * DegreesToRadians;
        var latitudeDifference = secondLatitude - firstLatitude;
        var longitudeDifference =
            (second.LongitudeDegrees - first.LongitudeDegrees) * DegreesToRadians;
        var sineLatitude = Math.Sin(latitudeDifference * 0.5);
        var sineLongitude = Math.Sin(longitudeDifference * 0.5);
        var haversine =
            (sineLatitude * sineLatitude) +
            (Math.Cos(firstLatitude) *
                Math.Cos(secondLatitude) *
                sineLongitude *
                sineLongitude);
        return 2 * sphereRadius *
            Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    /// <summary>
    /// Computes an initial great-circle bearing in degrees clockwise from
    /// north, in the range [0, 360).
    /// </summary>
    public static double InitialBearing(
        GeodeticCoordinate first,
        GeodeticCoordinate second) {
        var firstLatitude = first.LatitudeDegrees * DegreesToRadians;
        var secondLatitude = second.LatitudeDegrees * DegreesToRadians;
        var longitudeDifference =
            (second.LongitudeDegrees - first.LongitudeDegrees) * DegreesToRadians;
        var y = Math.Sin(longitudeDifference) * Math.Cos(secondLatitude);
        var x = (Math.Cos(firstLatitude) * Math.Sin(secondLatitude)) -
            (Math.Sin(firstLatitude) *
                Math.Cos(secondLatitude) *
                Math.Cos(longitudeDifference));
        var bearing = Math.Atan2(y, x) * RadiansToDegrees;
        return bearing < 0 ? bearing + 360 : bearing;
    }

    /// <summary>
    /// Solves the inverse geodesic problem on an ellipsoid using Vincenty's
    /// iteration.
    /// </summary>
    /// <remarks>
    /// The iteration does not converge for some nearly antipodal pairs. In
    /// those cases the method returns false with
    /// <see cref="CoordinateOperationStatus.NonConverged"/> and writes NaN to
    /// <paramref name="distance"/>; callers can choose a spherical
    /// approximation or a more specialized geodesic algorithm. Reference:
    /// Vincenty (1975),
    /// <see href="https://doi.org/10.1179/sre.1975.23.176.88">doi:10.1179/sre.1975.23.176.88</see>.
    /// </remarks>
    public static bool TryVincentyInverse(
        GeodeticCoordinate first,
        GeodeticCoordinate second,
        out double distance) {
        return TryVincentyInverse(
            first,
            second,
            Ellipsoid.Wgs84,
            out distance,
            out _);
    }

    /// <summary>
    /// Solves the inverse geodesic problem using WGS84 and returns a
    /// termination report.
    /// </summary>
    public static bool TryVincentyInverse(
        GeodeticCoordinate first,
        GeodeticCoordinate second,
        out double distance,
        out CoordinateOperationReport report) {
        return TryVincentyInverse(
            first,
            second,
            Ellipsoid.Wgs84,
            out distance,
            out report);
    }

    /// <summary>
    /// Solves the inverse geodesic problem on an ellipsoid.
    /// </summary>
    public static bool TryVincentyInverse(
        GeodeticCoordinate first,
        GeodeticCoordinate second,
        Ellipsoid ellipsoid,
        out double distance) {
        return TryVincentyInverse(first, second, ellipsoid, out distance, out _);
    }

    /// <summary>
    /// Solves the inverse geodesic problem on an ellipsoid and distinguishes
    /// convergence failure from a non-finite computed distance.
    /// </summary>
    public static bool TryVincentyInverse(
        GeodeticCoordinate first,
        GeodeticCoordinate second,
        Ellipsoid ellipsoid,
        out double distance,
        out CoordinateOperationReport report) {
        var semiMajor = ellipsoid.SemiMajorAxis;
        var semiMinor = ellipsoid.SemiMinorAxis;
        var flattening = ellipsoid.Flattening;
        var firstLatitude = first.LatitudeDegrees * DegreesToRadians;
        var secondLatitude = second.LatitudeDegrees * DegreesToRadians;
        var longitudeDifference =
            (second.LongitudeDegrees - first.LongitudeDegrees) * DegreesToRadians;
        var firstReduced = Math.Atan((1 - flattening) * Math.Tan(firstLatitude));
        var secondReduced = Math.Atan((1 - flattening) * Math.Tan(secondLatitude));
        var sineFirst = Math.Sin(firstReduced);
        var cosineFirst = Math.Cos(firstReduced);
        var sineSecond = Math.Sin(secondReduced);
        var cosineSecond = Math.Cos(secondReduced);
        var lambda = longitudeDifference;
        var previousLambda = 0.0;
        var sineSigma = 0.0;
        var cosineSigma = 0.0;
        var sigma = 0.0;
        var sineAlpha = 0.0;
        var cosineSquaredAlpha = 0.0;
        var cosineTwoSigmaMiddle = 0.0;
        var converged = false;
        for (var iteration = 0; iteration < VincentyMaximumIterations; iteration++) {
            var sineLambda = Math.Sin(lambda);
            var cosineLambda = Math.Cos(lambda);
            sineSigma = FloatingPointMath.Hypot(
                cosineSecond * sineLambda,
                (cosineFirst * sineSecond) -
                    (sineFirst * cosineSecond * cosineLambda));
            if (sineSigma == 0) {
                distance = 0;
                report = new CoordinateOperationReport(
                    CoordinateOperation.VincentyInverse,
                    CoordinateOperationStatus.Succeeded);
                return true;
            }

            cosineSigma =
                (sineFirst * sineSecond) +
                (cosineFirst * cosineSecond * cosineLambda);
            sigma = Math.Atan2(sineSigma, cosineSigma);
            sineAlpha = cosineFirst * cosineSecond * sineLambda / sineSigma;
            cosineSquaredAlpha = 1 - (sineAlpha * sineAlpha);
            cosineTwoSigmaMiddle = cosineSquaredAlpha == 0
                ? 0
                : cosineSigma -
                    ((2 * sineFirst * sineSecond) / cosineSquaredAlpha);
            var coefficient = (flattening / 16) *
                cosineSquaredAlpha *
                (4 + (flattening * (4 - (3 * cosineSquaredAlpha))));
            previousLambda = lambda;
            lambda = longitudeDifference +
                ((1 - coefficient) * flattening * sineAlpha *
                    (sigma + (coefficient * sineSigma *
                        (cosineTwoSigmaMiddle + (coefficient * cosineSigma *
                            (-1 + (2 * cosineTwoSigmaMiddle * cosineTwoSigmaMiddle)))))));
            if (Math.Abs(lambda - previousLambda) <= VincentyTolerance) {
                converged = true;
                break;
            }
        }

        if (!converged) {
            distance = double.NaN;
            report = new CoordinateOperationReport(
                CoordinateOperation.VincentyInverse,
                CoordinateOperationStatus.NonConverged);
            return false;
        }

        var uSquared = cosineSquaredAlpha *
            (semiMajor * semiMajor - semiMinor * semiMinor) /
            (semiMinor * semiMinor);
        var firstCoefficient = 1 + (uSquared / 16384) *
            (4096 + (uSquared *
                (-768 + (uSquared * (320 - (175 * uSquared))))));
        var secondCoefficient = (uSquared / 1024) *
            (256 + (uSquared *
                (-128 + (uSquared * (74 - (47 * uSquared))))));
        var middleSquared = cosineTwoSigmaMiddle * cosineTwoSigmaMiddle;
        var sigmaDifference = secondCoefficient * sineSigma *
            (cosineTwoSigmaMiddle +
                ((secondCoefficient / 4) *
                    ((cosineSigma *
                        (-1 + (2 * middleSquared))) -
                    ((secondCoefficient / 6) *
                        cosineTwoSigmaMiddle *
                        (-3 + (4 * sineSigma * sineSigma)) *
                        (-3 + (4 * middleSquared))))));
        distance = semiMinor * firstCoefficient *
            (sigma - sigmaDifference);
        if (!double.IsFinite(distance)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.VincentyInverse,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.VincentyInverse,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Solves the direct geodesic problem on an ellipsoid using Vincenty's
    /// formula.
    /// </summary>
    /// <remarks>
    /// Reference: Vincenty (1975),
    /// <see href="https://doi.org/10.1179/sre.1975.23.176.88">doi:10.1179/sre.1975.23.176.88</see>.
    /// </remarks>
    public static GeodeticCoordinate Destination(
        GeodeticCoordinate origin,
        double initialBearingDegrees,
        double distance) {
        return Destination(
            origin,
            initialBearingDegrees,
            distance,
            Ellipsoid.Wgs84);
    }

    public static GeodeticCoordinate Destination(
        GeodeticCoordinate origin,
        double initialBearingDegrees,
        double distance,
        Ellipsoid ellipsoid) {
        if (!double.IsFinite(initialBearingDegrees)) {
            throw new ArgumentOutOfRangeException(nameof(initialBearingDegrees));
        }

        if (!double.IsFinite(distance) || distance < 0) {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        if (distance == 0) {
            return origin;
        }

        var flattening = ellipsoid.Flattening;
        var semiMinor = ellipsoid.SemiMinorAxis;
        var alpha = initialBearingDegrees * DegreesToRadians;
        var latitude = origin.LatitudeDegrees * DegreesToRadians;
        var longitude = origin.LongitudeDegrees * DegreesToRadians;
        var tangentReduced = (1 - flattening) * Math.Tan(latitude);
        var cosineReduced = 1 / Math.Sqrt(1 + (tangentReduced * tangentReduced));
        var sineReduced = tangentReduced * cosineReduced;
        var sigmaOne = Math.Atan2(tangentReduced, Math.Cos(alpha));
        var sineAlpha = cosineReduced * Math.Sin(alpha);
        var cosineSquaredAlpha = 1 - (sineAlpha * sineAlpha);
        var uSquared = cosineSquaredAlpha *
            (ellipsoid.SemiMajorAxis * ellipsoid.SemiMajorAxis -
                semiMinor * semiMinor) /
            (semiMinor * semiMinor);
        var firstCoefficient = 1 + (uSquared / 16384) *
            (4096 + (uSquared *
                (-768 + (uSquared * (320 - (175 * uSquared))))));
        var secondCoefficient = (uSquared / 1024) *
            (256 + (uSquared *
                (-128 + (uSquared * (74 - (47 * uSquared))))));
        var sigma = distance / (semiMinor * firstCoefficient);
        var sineSigma = 0.0;
        var cosineSigma = 0.0;
        var cosineTwoSigmaMiddle = 0.0;
        for (var iteration = 0; iteration < VincentyMaximumIterations; iteration++) {
            cosineTwoSigmaMiddle = Math.Cos((2 * sigmaOne) + (2 * sigma));
            sineSigma = Math.Sin(sigma);
            cosineSigma = Math.Cos(sigma);
            var middleSquared = cosineTwoSigmaMiddle * cosineTwoSigmaMiddle;
            var sigmaDifference = secondCoefficient * sineSigma *
                (cosineTwoSigmaMiddle +
                    ((secondCoefficient / 4) *
                        ((cosineSigma *
                            (-1 + (2 * middleSquared))) -
                        ((secondCoefficient / 6) *
                            cosineTwoSigmaMiddle *
                            (-3 + (4 * sineSigma * sineSigma)) *
                            (-3 + (4 * middleSquared))))));
            var previousSigma = sigma;
            sigma = (distance / (semiMinor * firstCoefficient)) +
                sigmaDifference;
            if (Math.Abs(sigma - previousSigma) <= VincentyTolerance) {
                break;
            }
        }

        cosineTwoSigmaMiddle = Math.Cos((2 * sigmaOne) + (2 * sigma));
        sineSigma = Math.Sin(sigma);
        cosineSigma = Math.Cos(sigma);
        var temporary =
            (sineReduced * sineSigma) -
            (cosineReduced * cosineSigma * Math.Cos(alpha));
        var destinationLatitude = Math.Atan2(
            (sineReduced * cosineSigma) +
                (cosineReduced * sineSigma * Math.Cos(alpha)),
            (1 - flattening) *
                Math.Sqrt((sineAlpha * sineAlpha) + (temporary * temporary)));
        var lambda = Math.Atan2(
            sineSigma * Math.Sin(alpha),
            (cosineReduced * cosineSigma) -
                (sineReduced * sineSigma * Math.Cos(alpha)));
        var coefficient = (flattening / 16) *
            cosineSquaredAlpha *
            (4 + (flattening * (4 - (3 * cosineSquaredAlpha))));
        var longitudeDifference = lambda -
            ((1 - coefficient) * flattening * sineAlpha *
                (sigma + (coefficient * sineSigma *
                    (cosineTwoSigmaMiddle + (coefficient * cosineSigma *
                        (-1 + (2 * cosineTwoSigmaMiddle * cosineTwoSigmaMiddle)))))));
        var longitudeDegrees = NormalizeLongitude(
            longitude + longitudeDifference);
        return new GeodeticCoordinate(
            destinationLatitude * RadiansToDegrees,
            longitudeDegrees,
            origin.Height);
    }

    private static double NormalizeLongitude(double radians) {
        var degrees = radians * RadiansToDegrees;
        degrees %= 360;
        if (degrees < -180) {
            degrees += 360;
        } else if (degrees > 180) {
            degrees -= 360;
        }

        return degrees;
    }

    private static bool IsFinite(GeodeticCoordinate coordinate) {
        return double.IsFinite(coordinate.LatitudeDegrees) &&
            double.IsFinite(coordinate.LongitudeDegrees) &&
            double.IsFinite(coordinate.Height);
    }

    private static bool IsFinite(EcefCoordinate coordinate) {
        return double.IsFinite(coordinate.X) &&
            double.IsFinite(coordinate.Y) &&
            double.IsFinite(coordinate.Z);
    }

    private static void ThrowForFailure(CoordinateOperationReport report) {
        if (report.Status == CoordinateOperationStatus.InvalidInput) {
            throw new ArgumentException(
                "The ellipsoid parameters are invalid.");
        }

        if (report.Status == CoordinateOperationStatus.NonFiniteInput) {
            throw new ArgumentException(
                "The coordinate contains a non-finite component.");
        }

        throw new ArithmeticException(
            report.Status == CoordinateOperationStatus.NonConverged
                ? "The geodetic conversion did not converge."
                : "The geodetic conversion produced a non-finite result.");
    }
}
