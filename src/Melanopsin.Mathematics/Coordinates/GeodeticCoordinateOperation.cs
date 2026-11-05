namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A directed coordinate operation between two geodetic coordinate reference
/// systems.
/// </summary>
/// <remarks>
/// Operations carry their endpoints explicitly. A registry may expose both a
/// forward operation and its algebraic inverse, but the inverse is not
/// necessarily an independently published authority operation.
/// </remarks>
public abstract class GeodeticCoordinateOperation {
    /// <summary>Initializes a coordinate operation.</summary>
    protected GeodeticCoordinateOperation(
        GeodeticCrs source,
        GeodeticCrs target,
        string name,
        double? accuracyMeters = null) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException(
                "An operation name is required.",
                nameof(name));
        }

        if (accuracyMeters is { } accuracy &&
            (!double.IsFinite(accuracy) || accuracy < 0)) {
            throw new ArgumentOutOfRangeException(nameof(accuracyMeters));
        }

        Source = source;
        Target = target;
        Name = name;
        AccuracyMeters = accuracyMeters;
    }

    public GeodeticCrs Source { get; }

    public GeodeticCrs Target { get; }

    public string Name { get; }

    /// <summary>
    /// Gets the stated one-sigma accuracy in metres, when available. This is
    /// metadata supplied by the operation definition, not a computed bound.
    /// </summary>
    public double? AccuracyMeters { get; }

    /// <summary>
    /// Gets whether the operation provides an algebraic inverse.
    /// </summary>
    /// <remarks>
    /// A forward-only operation must leave this value <see langword="false"/>.
    /// This keeps path discovery from advertising an inverse that
    /// <see cref="TransformInverse"/> cannot execute.
    /// </remarks>
    public virtual bool IsInvertible => false;

    /// <summary>Transforms a source coordinate into the target system.</summary>
    public abstract GeodeticCoordinate Transform(GeodeticCoordinate source);

    /// <summary>
    /// Attempts to transform a source coordinate and reports input or numerical
    /// termination conditions.
    /// </summary>
    /// <remarks>
    /// This wrapper preserves exceptions that indicate programming errors. The
    /// forward operation's documented numeric and argument failures are
    /// translated into a report.
    /// </remarks>
    public virtual bool TryTransform(
        GeodeticCoordinate source,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        if (!IsFinite(source)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticForward,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        try {
            result = Transform(source);
            if (!IsFinite(result)) {
                result = default;
                report = new CoordinateOperationReport(
                    CoordinateOperation.GeodeticForward,
                    CoordinateOperationStatus.NonFiniteOutput);
                return false;
            }

            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticForward,
                CoordinateOperationStatus.Succeeded);
            return true;
        } catch (ArithmeticException) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticForward,
                CoordinateOperationStatus.NumericalFailure);
            return false;
        }
    }

    /// <summary>
    /// Applies the algebraic inverse of this operation.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The operation is not invertible.
    /// </exception>
    public virtual GeodeticCoordinate TransformInverse(
        GeodeticCoordinate target) {
        throw new NotSupportedException(
            "This coordinate operation is not invertible.");
    }

    /// <summary>
    /// Attempts to apply the algebraic inverse and reports why it is
    /// unavailable or failed.
    /// </summary>
    /// <remarks>
    /// A forward-only operation reports
    /// <see cref="CoordinateOperationStatus.NotInvertible"/> without invoking
    /// <see cref="TransformInverse"/>. Numeric and input failures from an
    /// invertible operation are translated into a report.
    /// </remarks>
    public virtual bool TryTransformInverse(
        GeodeticCoordinate target,
        out GeodeticCoordinate result,
        out CoordinateOperationReport report) {
        if (!IsInvertible) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticInverse,
                CoordinateOperationStatus.NotInvertible);
            return false;
        }

        if (!IsFinite(target)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticInverse,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        try {
            result = TransformInverse(target);
            if (!IsFinite(result)) {
                result = default;
                report = new CoordinateOperationReport(
                    CoordinateOperation.GeodeticInverse,
                    CoordinateOperationStatus.NonFiniteOutput);
                return false;
            }

            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticInverse,
                CoordinateOperationStatus.Succeeded);
            return true;
        } catch (ArithmeticException) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticInverse,
                CoordinateOperationStatus.NumericalFailure);
            return false;
        }
    }

    private static bool IsFinite(GeodeticCoordinate coordinate) {
        return double.IsFinite(coordinate.LatitudeDegrees) &&
            double.IsFinite(coordinate.LongitudeDegrees) &&
            double.IsFinite(coordinate.Height);
    }
}
