// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A geodetic coordinate operation backed by a seven-parameter Helmert
/// transformation in geocentric Cartesian space.
/// </summary>
public sealed class HelmertCoordinateOperation : GeodeticCoordinateOperation {
    private readonly GeodeticDatumTransform _datumTransform;

    /// <summary>
    /// Initializes a Helmert geodetic coordinate operation.
    /// </summary>
    public HelmertCoordinateOperation(
        GeodeticCrs source,
        GeodeticCrs target,
        HelmertTransform geocentricTransform,
        string? name = null,
        double? accuracyMeters = null)
        : base(
            source,
            target,
            name ?? "Seven-parameter Helmert transformation",
            accuracyMeters) {
        _datumTransform = new GeodeticDatumTransform(
            source.Datum.Ellipsoid,
            target.Datum.Ellipsoid,
            geocentricTransform);
    }

    public HelmertTransform GeocentricTransform =>
        _datumTransform.GeocentricTransform;

    public override bool IsInvertible => true;

    public override GeodeticCoordinate Transform(
        GeodeticCoordinate source) {
        return _datumTransform.Transform(source);
    }

    public override GeodeticCoordinate TransformInverse(
        GeodeticCoordinate target) {
        return _datumTransform.TransformInverse(target);
    }

    public override bool TryTransform(
        GeodeticCoordinate source,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        return _datumTransform.TryTransform(source, out result, out report);
    }

    public override bool TryTransformInverse(
        GeodeticCoordinate target,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        return _datumTransform.TryTransformInverse(target, out result, out report);
    }
}
