// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Mathematics.Coordinates;

namespace Melanopsin.Mathematics.Optics;

/// <summary>Identifies an optical operation that can terminate without a result.</summary>
public enum OpticsOperation {
    /// <summary>Construction of a geometrical ray.</summary>
    RayConstruction,

    /// <summary>Evaluation of a point on a geometrical ray.</summary>
    RayPointEvaluation,

    /// <summary>Construction of a frame attached to a geometrical ray.</summary>
    RayFrameConstruction,

    /// <summary>Coordinate-frame transformation of a geometrical ray.</summary>
    RayFrameTransform,

    /// <summary>Inverse of a paraxial ray-transfer matrix.</summary>
    RayTransferInverse,

    /// <summary>Normalization of a Jones vector.</summary>
    JonesNormalization,

    /// <summary>Inverse of a Jones matrix.</summary>
    JonesInverse,

    /// <summary>Evaluation of the degree of polarization.</summary>
    StokesDegreeOfPolarization,
}

/// <summary>The termination status of an optical operation.</summary>
public enum OpticsOperationStatus {
    /// <summary>The operation produced its defined result.</summary>
    Succeeded,

    /// <summary>An input component is NaN or infinite where a finite value is required.</summary>
    NonFiniteInput,

    /// <summary>A direction or transverse amplitude is zero, so normalization is undefined.</summary>
    DegenerateInput,

    /// <summary>A probability, scale, or other parameter is outside its mathematical domain.</summary>
    InvalidParameter,

    /// <summary>A required determinant or scale is exactly zero in the working precision.</summary>
    Singular,

    /// <summary>A finite input produced a non-finite result.</summary>
    NonFiniteOutput,

    /// <summary>The mathematical result is undefined for the supplied state.</summary>
    Undefined,

    /// <summary>The operation failed numerically for a reason other than singularity.</summary>
    NumericalFailure,
}

/// <summary>
/// A compact diagnostic report shared by geometrical, paraxial, and
/// polarization optical operations.
/// </summary>
/// <remarks>
/// <para>
/// No absolute near-singularity or near-degeneracy threshold is applied by
/// these operations. Singularity means exact zero in the working precision;
/// callers that need an accuracy bound must request a scale-aware condition
/// estimate or define an application-specific acceptance criterion.
/// </para>
/// <para>
/// Non-finite results are not automatically classified as errors because some
/// optical models intentionally represent limiting cases with infinity. The
/// individual operation contract states whether infinity is a defined result.
/// </para>
/// </remarks>
public readonly record struct OpticsOperationReport {
    internal OpticsOperationReport(
        OpticsOperation operation,
        OpticsOperationStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the reported operation.</summary>
    public OpticsOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public OpticsOperationStatus Status { get; }

    /// <summary>Gets whether the operation produced its defined result.</summary>
    public bool Succeeded => Status == OpticsOperationStatus.Succeeded;
}

public readonly partial record struct OpticalRay {
    /// <summary>
    /// Attempts to construct a normalized geometrical ray and reports whether
    /// the origin or direction prevented construction.
    /// </summary>
    public static bool TryCreate(
        CartesianCoordinate3 origin,
        Vector3<double> direction,
        out OpticalRay ray,
        out OpticsOperationReport report) {
        if (!IsFinite(origin)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayConstruction,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CartesianVector3Math.IsFinite(direction)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayConstruction,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CartesianVector3Math.TryNormalize(
                direction,
                out _,
                out var inverseLength)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayConstruction,
                OpticsOperationStatus.DegenerateInput);
            return false;
        }

        ray = new OpticalRay(origin, direction, inverseLength);
        report = new OpticsOperationReport(
            OpticsOperation.RayConstruction,
            OpticsOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to evaluate a point on the ray and reports non-finite or
    /// unrepresentable input and output.
    /// </summary>
    public bool TryPointAt(
        double distance,
        out CartesianCoordinate3 result,
        out OpticsOperationReport report) {
        if (!IsFinite(Origin) || !IsFinite(Direction)) {
            result = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayPointEvaluation,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (!double.IsFinite(distance)) {
            result = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayPointEvaluation,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        var x = Origin.X + (Direction.X * distance);
        var y = Origin.Y + (Direction.Y * distance);
        var z = Origin.Z + (Direction.Z * distance);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) {
            result = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayPointEvaluation,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new CartesianCoordinate3(x, y, z);
        report = new OpticsOperationReport(
            OpticsOperation.RayPointEvaluation,
            OpticsOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to construct a ray-local frame and reports degenerate geometry
    /// separately from non-finite input.
    /// </summary>
    public bool TryCreateLocalFrame(
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out OpticsOperationReport report) {
        if (!IsFinite(Origin) ||
            !IsFinite(Direction) ||
            !CartesianVector3Math.IsFinite(transverseXAxisHint)) {
            frame = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameConstruction,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CoordinateFrame3.TryCreateFromZAxis(
                Origin,
                Direction,
                transverseXAxisHint,
                handedness,
                out frame,
                out var coordinateReport)) {
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameConstruction,
                Map(coordinateReport.Status));
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.RayFrameConstruction,
            OpticsOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to construct a ray-local frame at a point and reports the first
    /// failing stage.
    /// </summary>
    public bool TryCreateLocalFrameAt(
        double distance,
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out OpticsOperationReport report) {
        if (!TryPointAt(distance, out var origin, out var pointReport)) {
            frame = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameConstruction,
                pointReport.Status == OpticsOperationStatus.NonFiniteInput
                    ? OpticsOperationStatus.NonFiniteInput
                    : OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!CoordinateFrame3.TryCreateFromZAxis(
                origin,
                Direction,
                transverseXAxisHint,
                handedness,
                out frame,
                out var coordinateReport)) {
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameConstruction,
                Map(coordinateReport.Status));
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.RayFrameConstruction,
            OpticsOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts to transform the ray between coordinate frames and reports the
    /// first failing stage.
    /// </summary>
    public bool TryTransform(
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out OpticalRay ray,
        out OpticsOperationReport report) {
        if (!IsFinite(Origin) || !IsFinite(Direction)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameTransform,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CoordinateFrame3.TryTransformPoint(
                Origin,
                source,
                target,
                out var origin,
                out var pointReport)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameTransform,
                Map(pointReport.Status));
            return false;
        }

        if (!CoordinateFrame3.TryTransformDirection(
                Direction,
                source,
                target,
                out var direction,
                out var directionReport)) {
            ray = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayFrameTransform,
                Map(directionReport.Status));
            return false;
        }

        return TryCreate(origin, direction, out ray, out report);
    }

    private static bool IsFinite(CartesianCoordinate3 value) {
        return double.IsFinite(value.X) &&
            double.IsFinite(value.Y) &&
            double.IsFinite(value.Z);
    }

    private static bool IsFinite(Vector3<double> value) {
        return CartesianVector3Math.IsFinite(value);
    }

    internal static OpticsOperationStatus Map(
        CoordinateOperationStatus status) {
        return status switch {
            CoordinateOperationStatus.Succeeded => OpticsOperationStatus.Succeeded,
            CoordinateOperationStatus.UnknownSource or
                CoordinateOperationStatus.UnknownTarget or
                CoordinateOperationStatus.NoPath or
                CoordinateOperationStatus.NotInvertible =>
                OpticsOperationStatus.Undefined,
            CoordinateOperationStatus.InvalidInput or
                CoordinateOperationStatus.OutOfDomain =>
                OpticsOperationStatus.InvalidParameter,
            CoordinateOperationStatus.DegenerateGeometry =>
                OpticsOperationStatus.DegenerateInput,
            CoordinateOperationStatus.NonFiniteInput =>
                OpticsOperationStatus.NonFiniteInput,
            CoordinateOperationStatus.NonFiniteOutput =>
                OpticsOperationStatus.NonFiniteOutput,
            CoordinateOperationStatus.Singular => OpticsOperationStatus.Singular,
            CoordinateOperationStatus.NumericalFailure or
                CoordinateOperationStatus.NonConverged =>
                OpticsOperationStatus.NumericalFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown coordinate operation status."),
        };
    }
}

public readonly partial record struct RayTransferMatrix {
    /// <summary>
    /// Attempts to invert the matrix and reports singular, non-finite, and
    /// numerically failed cases without an absolute near-singularity threshold.
    /// </summary>
    public bool TryInvert(
        out RayTransferMatrix inverse,
        out OpticsOperationReport report) {
        if (!IsFinite) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayTransferInverse,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        var determinant = Determinant;
        if (determinant == 0) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayTransferInverse,
                OpticsOperationStatus.Singular);
            return false;
        }

        if (!double.IsFinite(determinant)) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayTransferInverse,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        var reciprocal = 1 / determinant;
        inverse = new RayTransferMatrix(
            D * reciprocal,
            -B * reciprocal,
            -C * reciprocal,
            A * reciprocal);
        if (!inverse.IsFinite) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.RayTransferInverse,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.RayTransferInverse,
            OpticsOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct JonesVector {
    /// <summary>
    /// Attempts to normalize the vector and reports zero/non-finite intensity
    /// separately from non-finite components.
    /// </summary>
    public bool TryNormalize(
        out JonesVector result,
        out OpticsOperationReport report) {
        if (!IsFinite) {
            result = Zero;
            report = new OpticsOperationReport(
                OpticsOperation.JonesNormalization,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        var intensity = Intensity;
        if (!double.IsFinite(intensity)) {
            result = Zero;
            report = new OpticsOperationReport(
                OpticsOperation.JonesNormalization,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!(intensity > 0)) {
            result = Zero;
            report = new OpticsOperationReport(
                OpticsOperation.JonesNormalization,
                OpticsOperationStatus.DegenerateInput);
            return false;
        }

        var scale = 1 / Math.Sqrt(intensity);
        result = new JonesVector(X * scale, Y * scale);
        if (!result.IsFinite) {
            result = Zero;
            report = new OpticsOperationReport(
                OpticsOperation.JonesNormalization,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.JonesNormalization,
            OpticsOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct JonesMatrix {
    /// <summary>
    /// Attempts to invert the matrix and reports exact singularity separately
    /// from non-finite input and output.
    /// </summary>
    public bool TryInvert(
        out JonesMatrix inverse,
        out OpticsOperationReport report) {
        if (!IsFinite) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.JonesInverse,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        var determinant = Determinant;
        if (!determinant.IsFinite) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.JonesInverse,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        if (determinant.Real == 0 && determinant.Imaginary == 0) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.JonesInverse,
                OpticsOperationStatus.Singular);
            return false;
        }

        inverse = new JonesMatrix(
            YY / determinant,
            -XY / determinant,
            -YX / determinant,
            XX / determinant);
        if (!inverse.IsFinite) {
            inverse = default;
            report = new OpticsOperationReport(
                OpticsOperation.JonesInverse,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.JonesInverse,
            OpticsOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct StokesVector {
    /// <summary>
    /// Attempts to evaluate the degree of polarization and reports zero
    /// intensity, non-finite input, and non-finite output explicitly.
    /// </summary>
    public bool TryGetDegreeOfPolarization(
        out double degreeOfPolarization,
        out OpticsOperationReport report) {
        if (!IsFinite) {
            degreeOfPolarization = double.NaN;
            report = new OpticsOperationReport(
                OpticsOperation.StokesDegreeOfPolarization,
                OpticsOperationStatus.NonFiniteInput);
            return false;
        }

        if (I == 0) {
            degreeOfPolarization = double.NaN;
            report = new OpticsOperationReport(
                OpticsOperation.StokesDegreeOfPolarization,
                OpticsOperationStatus.Undefined);
            return false;
        }

        var polarized = Math.Sqrt((Q * Q) + (U * U) + (V * V));
        degreeOfPolarization = polarized / Math.Abs(I);
        if (!double.IsFinite(degreeOfPolarization)) {
            degreeOfPolarization = double.NaN;
            report = new OpticsOperationReport(
                OpticsOperation.StokesDegreeOfPolarization,
                OpticsOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new OpticsOperationReport(
            OpticsOperation.StokesDegreeOfPolarization,
            OpticsOperationStatus.Succeeded);
        return true;
    }
}
