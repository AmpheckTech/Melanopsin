namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A geodetic datum transformation composed from ellipsoid conversions and a
/// seven-parameter geocentric Helmert transform.
/// </summary>
/// <remarks>
/// <para>
/// A coordinate is converted from source geodetic coordinates to source ECEF,
/// transformed into the target ECEF frame, and then converted to target
/// geodetic coordinates using the target ellipsoid. Heights are ellipsoidal,
/// not orthometric.
/// </para>
/// <para>
/// This type does not identify datums, select transformation parameters, or
/// apply coordinate-epoch and crustal-motion models. The caller is responsible
/// for supplying the authoritative parameters and direction for the region and
/// epoch of the data. A matching ellipsoid alone never proves that two datums
/// share an Earth-fixed frame.
/// </para>
/// </remarks>
public readonly record struct GeodeticDatumTransform {
    /// <summary>
    /// Initializes a geodetic datum transformation.
    /// </summary>
    public GeodeticDatumTransform(
        Ellipsoid sourceEllipsoid,
        Ellipsoid targetEllipsoid,
        HelmertTransform geocentricTransform) {
        SourceEllipsoid = sourceEllipsoid;
        TargetEllipsoid = targetEllipsoid;
        GeocentricTransform = geocentricTransform;
    }

    public Ellipsoid SourceEllipsoid { get; }

    public Ellipsoid TargetEllipsoid { get; }

    public HelmertTransform GeocentricTransform { get; }

    /// <summary>
    /// Creates a conversion between ellipsoid representations that are known
    /// to use the same Earth-fixed reference frame.
    /// </summary>
    /// <remarks>
    /// The Helmert transform is the identity. Use this only when the source
    /// and target coordinate systems share a frame; otherwise provide the
    /// applicable datum transformation parameters to the constructor.
    /// </remarks>
    public static GeodeticDatumTransform SameFrame(
        Ellipsoid sourceEllipsoid,
        Ellipsoid targetEllipsoid) {
        return new GeodeticDatumTransform(
            sourceEllipsoid,
            targetEllipsoid,
            HelmertTransform.Identity);
    }

    /// <summary>
    /// Transforms a source geodetic coordinate into the target datum and
    /// ellipsoid.
    /// </summary>
    public GeodeticCoordinate Transform(GeodeticCoordinate source) {
        if (!TryTransform(source, out var result, out var report)) {
            ThrowForFailure(report);
        }

        return result;
    }

    /// <summary>
    /// Applies the algebraic inverse of this composite transformation.
    /// </summary>
    /// <remarks>
    /// This does not select an independently published reverse datum
    /// transformation. It exactly inverts the configured source-to-target
    /// calculation before converting to the source ellipsoid.
    /// </remarks>
    public GeodeticCoordinate TransformInverse(GeodeticCoordinate target) {
        if (!TryTransformInverse(target, out var result, out var report)) {
            ThrowForFailure(report);
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a source geodetic coordinate into the target datum
    /// and reports whether the input, parameters, or result prevented a finite
    /// conversion.
    /// </summary>
    /// <remarks>
    /// A successful report confirms only that a finite result was produced. It
    /// does not certify datum suitability, parameter authority, accuracy, or
    /// conditioning; those remain properties of the supplied definition and
    /// the caller's application context.
    /// </remarks>
    public bool TryTransform(
        GeodeticCoordinate source,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        return TryTransformCore(
            source,
            inverse: false,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to apply the algebraic inverse and report input, parameter, or
    /// numerical termination conditions.
    /// </summary>
    public bool TryTransformInverse(
        GeodeticCoordinate target,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        return TryTransformCore(
            target,
            inverse: true,
            out result,
            out report);
    }

    private bool TryTransformCore(
        GeodeticCoordinate coordinate,
        bool inverse,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        var operation = inverse
            ? CoordinateOperation.DatumInverse
            : CoordinateOperation.DatumForward;
        result = default;
        if (!IsFinite(coordinate)) {
            report = new CoordinateOperationReport(
                operation,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!SourceEllipsoid.IsValid || !TargetEllipsoid.IsValid) {
            report = new CoordinateOperationReport(
                operation,
                CoordinateOperationStatus.InvalidInput,
                0,
                inverse ? 1 : 0);
            return false;
        }

        var sourceEllipsoid = inverse ? TargetEllipsoid : SourceEllipsoid;
        var targetEllipsoid = inverse ? SourceEllipsoid : TargetEllipsoid;
        if (!Geodesy.TryToEcef(
                coordinate,
                sourceEllipsoid,
                out var sourceEcef,
                out var ecefReport)) {
            report = new CoordinateOperationReport(
                operation,
                ecefReport.Status,
                0,
                inverse ? 1 : 0);
            return false;
        }

        var helmertSucceeded = inverse
            ? GeocentricTransform.TryTransformInverse(
                sourceEcef,
                out var targetEcef,
                out var helmertReport)
            : GeocentricTransform.TryTransform(
                sourceEcef,
                out targetEcef,
                out helmertReport);
        if (!helmertSucceeded) {
            report = new CoordinateOperationReport(
                operation,
                helmertReport.Status,
                0,
                inverse ? 1 : 0);
            return false;
        }

        if (!Geodesy.TryToGeodetic(
                targetEcef,
                targetEllipsoid,
                out result,
                out var geodeticReport)) {
            result = default;
            report = new CoordinateOperationReport(
                operation,
                geodeticReport.Status,
                0,
                inverse ? 1 : 0);
            return false;
        }

        report = new CoordinateOperationReport(
            operation,
            CoordinateOperationStatus.Succeeded,
            inverse ? 0 : 1,
            inverse ? 1 : 0);
        return true;
    }

    private static bool IsFinite(GeodeticCoordinate coordinate) {
        return double.IsFinite(coordinate.LatitudeDegrees) &&
            double.IsFinite(coordinate.LongitudeDegrees) &&
            double.IsFinite(coordinate.Height);
    }

    private static void ThrowForFailure(CoordinateOperationReport report) {
        if (report.Status == CoordinateOperationStatus.InvalidInput) {
            throw new ArgumentException(
                "The source or target ellipsoid is invalid.");
        }

        if (report.Status == CoordinateOperationStatus.NonFiniteInput) {
            throw new ArgumentException(
                "The coordinate contains a non-finite component.");
        }

        throw new ArithmeticException(
            "The datum transformation could not produce a finite result.");
    }
}
