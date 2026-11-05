// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// An orthonormal three-dimensional coordinate frame expressed in a reference
/// frame.
/// </summary>
/// <remarks>
/// The frame consists of an origin and three mutually orthogonal unit axes.
/// Right-handed frames satisfy <c>Z = X cross Y</c>; left-handed frames satisfy
/// <c>Z = Y cross X</c>. Coordinates are unit-neutral, so every point, length,
/// and direction in a transform must use the same physical unit.
/// <para>
/// The type stores a pose only. It does not store a parent frame, a unit, or an
/// element identity. The reference frame is the coordinate system in which
/// <see cref="Origin"/> and the axes are expressed. A global frame may therefore
/// be represented by <see cref="Identity"/> by convention, while local element,
/// surface, and ray frames use the same value type with explicit transforms.
/// </para>
/// <para>
/// Construction regularizes axis directions by normalization and orthogonal
/// projection. Shear and non-unit scale cannot be represented and should be
/// applied as separate matrix operations rather than hidden inside a frame.
/// </para>
/// </remarks>
public readonly record struct CoordinateFrame3 {
    private static readonly CoordinateFrame3 IdentityFrame = new(
        new CartesianCoordinate3(0, 0, 0),
        Vector3<double>.UnitX,
        Vector3<double>.UnitY,
        Vector3<double>.UnitZ,
        CoordinateHandedness.RightHanded);

    private CoordinateFrame3(
        CartesianCoordinate3 origin,
        Vector3<double> xAxis,
        Vector3<double> yAxis,
        Vector3<double> zAxis,
        CoordinateHandedness handedness) {
        Origin = origin;
        XAxis = xAxis;
        YAxis = yAxis;
        ZAxis = zAxis;
        Handedness = handedness;
    }

    /// <summary>
    /// Gets the canonical global frame: origin at zero with identity axes in a
    /// right-handed orientation.
    /// </summary>
    public static CoordinateFrame3 Identity => IdentityFrame;

    public CartesianCoordinate3 Origin { get; }

    public Vector3<double> XAxis { get; }

    public Vector3<double> YAxis { get; }

    public Vector3<double> ZAxis { get; }

    public CoordinateHandedness Handedness { get; }

    /// <summary>
    /// Constructs a frame from an X axis and an independent Y-axis direction.
    /// </summary>
    /// <remarks>
    /// The X axis is normalized. The Y axis is then made orthogonal to X and
    /// normalized. The Z axis is derived from the requested handedness, so the
    /// result is always orthonormal. This is a Gram-Schmidt construction rather
    /// than an absolute tolerance test for near-parallel axes.
    /// </remarks>
    public static CoordinateFrame3 Create(
        CartesianCoordinate3 origin,
        Vector3<double> xAxis,
        Vector3<double> yAxis,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        if (!TryCreate(origin, xAxis, yAxis, handedness, out var frame)) {
            throw new ArgumentException(
                "The axes must be finite and define two independent directions.");
        }

        return frame;
    }

    /// <summary>
    /// Attempts to construct an orthonormal frame from X and Y-axis directions.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when both axes are finite and non-parallel;
    /// otherwise, <see langword="false"/> with <paramref name="frame"/> set to
    /// <see langword="default"/>.
    /// </returns>
    public static bool TryCreate(
        CartesianCoordinate3 origin,
        Vector3<double> xAxis,
        Vector3<double> yAxis,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreate(origin, xAxis, yAxis, handedness, out frame, out _);
    }

    /// <summary>
    /// Attempts to construct an orthonormal frame and reports the terminating
    /// input or numerical condition.
    /// </summary>
    public static bool TryCreate(
        CartesianCoordinate3 origin,
        Vector3<double> xAxis,
        Vector3<double> yAxis,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out CoordinateOperationReport report) {
        frame = default;
        if (!IsHandednessValid(handedness)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromAxes,
                CoordinateOperationStatus.InvalidInput);
            return false;
        }

        if (!IsFinite(origin) ||
            !CartesianVector3Math.IsFinite(xAxis) ||
            !CartesianVector3Math.IsFinite(yAxis)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromAxes,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CartesianVector3Math.TryNormalize(xAxis, out var unitX) ||
            !TryOrthogonalize(yAxis, unitX, out var unitY)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromAxes,
                CoordinateOperationStatus.DegenerateGeometry);
            return false;
        }

        var unitZ = handedness == CoordinateHandedness.RightHanded
            ? Vector3<double>.Cross(unitX, unitY)
            : Vector3<double>.Cross(unitY, unitX);
        frame = new CoordinateFrame3(
            origin,
            unitX,
            unitY,
            unitZ,
            handedness);
        report = new CoordinateOperationReport(
            CoordinateOperation.FrameFromAxes,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Constructs a frame with an exact Z axis and an X axis projected from the
    /// supplied hint.
    /// </summary>
    /// <remarks>
    /// This construction is useful for optical surfaces and rays: local positive
    /// Z carries the surface normal or propagation direction, while the X-axis
    /// hint fixes the transverse orientation. For a right-handed frame
    /// <c>Y = Z cross X</c>; for a left-handed frame <c>Y = X cross Z</c>.
    /// </remarks>
    public static CoordinateFrame3 CreateFromZAxis(
        CartesianCoordinate3 origin,
        Vector3<double> zAxis,
        Vector3<double> xAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        if (!TryCreateFromZAxis(
            origin,
            zAxis,
            xAxisHint,
            handedness,
            out var frame)) {
            throw new ArgumentException(
                "The Z axis and X-axis hint must be finite and non-parallel.");
        }

        return frame;
    }

    /// <summary>
    /// Attempts to construct a frame with an exact Z axis and projected X-axis
    /// hint.
    /// </summary>
    public static bool TryCreateFromZAxis(
        CartesianCoordinate3 origin,
        Vector3<double> zAxis,
        Vector3<double> xAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreateFromZAxis(
            origin,
            zAxis,
            xAxisHint,
            handedness,
            out frame,
            out _);
    }

    /// <summary>
    /// Attempts to construct a frame with an exact Z axis and reports the
    /// terminating input or numerical condition.
    /// </summary>
    public static bool TryCreateFromZAxis(
        CartesianCoordinate3 origin,
        Vector3<double> zAxis,
        Vector3<double> xAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out CoordinateOperationReport report) {
        frame = default;
        if (!IsHandednessValid(handedness)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromZAxis,
                CoordinateOperationStatus.InvalidInput);
            return false;
        }

        if (!IsFinite(origin) ||
            !CartesianVector3Math.IsFinite(zAxis) ||
            !CartesianVector3Math.IsFinite(xAxisHint)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromZAxis,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!CartesianVector3Math.TryNormalize(zAxis, out var unitZ) ||
            !TryOrthogonalize(xAxisHint, unitZ, out var unitX)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameFromZAxis,
                CoordinateOperationStatus.DegenerateGeometry);
            return false;
        }

        var unitY = handedness == CoordinateHandedness.RightHanded
            ? Vector3<double>.Cross(unitZ, unitX)
            : Vector3<double>.Cross(unitX, unitZ);
        frame = new CoordinateFrame3(
            origin,
            unitX,
            unitY,
            unitZ,
            handedness);
        report = new CoordinateOperationReport(
            CoordinateOperation.FrameFromZAxis,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Transforms a point from this local frame into its reference frame.
    /// </summary>
    public CartesianCoordinate3 PointToReference(CartesianCoordinate3 point) {
        if (!TryPointToReference(point, out var result)) {
            throw new ArithmeticException(
                "The transformed point is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a point from this local frame into its reference
    /// frame.
    /// </summary>
    public bool TryPointToReference(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result) {
        return TryPointToReference(point, out result, out _);
    }

    private bool TryPointToReferenceCore(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result) {
        return TryCreatePoint(
            Origin.X +
                (XAxis.X * point.X) +
                (YAxis.X * point.Y) +
                (ZAxis.X * point.Z),
            Origin.Y +
                (XAxis.Y * point.X) +
                (YAxis.Y * point.Y) +
                (ZAxis.Y * point.Z),
            Origin.Z +
                (XAxis.Z * point.X) +
                (YAxis.Z * point.Y) +
                (ZAxis.Z * point.Z),
            out result);
    }

    /// <summary>
    /// Attempts to transform a local point and reports invalid input or a
    /// non-finite output.
    /// </summary>
    public bool TryPointToReference(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        if (!IsFinite(point) || !IsFrameFinite(this)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.PointToReference,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var succeeded = TryPointToReferenceCore(point, out result);
        report = new CoordinateOperationReport(
            CoordinateOperation.PointToReference,
            succeeded
                ? CoordinateOperationStatus.Succeeded
                : CoordinateOperationStatus.NonFiniteOutput);
        return succeeded;
    }

    /// <summary>
    /// Transforms a point from the reference frame into this local frame.
    /// </summary>
    public CartesianCoordinate3 PointFromReference(CartesianCoordinate3 point) {
        if (!TryPointFromReference(point, out var result)) {
            throw new ArithmeticException(
                "The transformed point is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a point from the reference frame into this local
    /// frame.
    /// </summary>
    public bool TryPointFromReference(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result) {
        return TryPointFromReference(point, out result, out _);
    }

    private bool TryPointFromReferenceCore(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result) {
        var offsetX = point.X - Origin.X;
        var offsetY = point.Y - Origin.Y;
        var offsetZ = point.Z - Origin.Z;
        return TryCreatePoint(
            (XAxis.X * offsetX) +
                (XAxis.Y * offsetY) +
                (XAxis.Z * offsetZ),
            (YAxis.X * offsetX) +
                (YAxis.Y * offsetY) +
                (YAxis.Z * offsetZ),
            (ZAxis.X * offsetX) +
                (ZAxis.Y * offsetY) +
                (ZAxis.Z * offsetZ),
            out result);
    }

    /// <summary>
    /// Attempts to transform a reference-frame point and reports invalid input
    /// or a non-finite output.
    /// </summary>
    public bool TryPointFromReference(
        CartesianCoordinate3 point,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        if (!IsFinite(point) || !IsFrameFinite(this)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.PointFromReference,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var succeeded = TryPointFromReferenceCore(point, out result);
        report = new CoordinateOperationReport(
            CoordinateOperation.PointFromReference,
            succeeded
                ? CoordinateOperationStatus.Succeeded
                : CoordinateOperationStatus.NonFiniteOutput);
        return succeeded;
    }

    /// <summary>
    /// Rotates a direction from this local frame into its reference frame.
    /// </summary>
    public Vector3<double> DirectionToReference(Vector3<double> direction) {
        if (!TryDirectionToReference(direction, out var result)) {
            throw new ArithmeticException(
                "The transformed direction is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to rotate a direction from this local frame into its reference
    /// frame.
    /// </summary>
    public bool TryDirectionToReference(
        Vector3<double> direction,
        out Vector3<double> result) {
        return TryDirectionToReference(direction, out result, out _);
    }

    private bool TryDirectionToReferenceCore(
        Vector3<double> direction,
        out Vector3<double> result) {
        result = new Vector3<double>(
            (XAxis.X * direction.X) +
                (YAxis.X * direction.Y) +
                (ZAxis.X * direction.Z),
            (XAxis.Y * direction.X) +
                (YAxis.Y * direction.Y) +
                (ZAxis.Y * direction.Z),
            (XAxis.Z * direction.X) +
                (YAxis.Z * direction.Y) +
                (ZAxis.Z * direction.Z));
        return CartesianVector3Math.IsFinite(result);
    }

    /// <summary>
    /// Attempts to rotate a local direction and reports invalid input or a
    /// non-finite output.
    /// </summary>
    public bool TryDirectionToReference(
        Vector3<double> direction,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        if (!CartesianVector3Math.IsFinite(direction) || !IsFrameFinite(this)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.DirectionToReference,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var succeeded = TryDirectionToReferenceCore(direction, out result);
        report = new CoordinateOperationReport(
            CoordinateOperation.DirectionToReference,
            succeeded
                ? CoordinateOperationStatus.Succeeded
                : CoordinateOperationStatus.NonFiniteOutput);
        return succeeded;
    }

    /// <summary>
    /// Rotates a direction from the reference frame into this local frame.
    /// </summary>
    public Vector3<double> DirectionFromReference(Vector3<double> direction) {
        if (!TryDirectionFromReference(direction, out var result)) {
            throw new ArithmeticException(
                "The transformed direction is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to rotate a direction from the reference frame into this local
    /// frame.
    /// </summary>
    public bool TryDirectionFromReference(
        Vector3<double> direction,
        out Vector3<double> result) {
        return TryDirectionFromReference(direction, out result, out _);
    }

    private bool TryDirectionFromReferenceCore(
        Vector3<double> direction,
        out Vector3<double> result) {
        result = new Vector3<double>(
            (XAxis.X * direction.X) +
                (XAxis.Y * direction.Y) +
                (XAxis.Z * direction.Z),
            (YAxis.X * direction.X) +
                (YAxis.Y * direction.Y) +
                (YAxis.Z * direction.Z),
            (ZAxis.X * direction.X) +
                (ZAxis.Y * direction.Y) +
                (ZAxis.Z * direction.Z));
        return CartesianVector3Math.IsFinite(result);
    }

    /// <summary>
    /// Attempts to rotate a reference-frame direction and reports invalid input
    /// or a non-finite output.
    /// </summary>
    public bool TryDirectionFromReference(
        Vector3<double> direction,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        if (!CartesianVector3Math.IsFinite(direction) || !IsFrameFinite(this)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.DirectionFromReference,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var succeeded = TryDirectionFromReferenceCore(direction, out result);
        report = new CoordinateOperationReport(
            CoordinateOperation.DirectionFromReference,
            succeeded
                ? CoordinateOperationStatus.Succeeded
                : CoordinateOperationStatus.NonFiniteOutput);
        return succeeded;
    }

    /// <summary>
    /// Transforms a point between two frames that share the same reference
    /// frame.
    /// </summary>
    public static CartesianCoordinate3 TransformPoint(
        CartesianCoordinate3 point,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        if (!TryTransformPoint(point, source, target, out var result)) {
            throw new ArithmeticException(
                "The transformed point is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a point between two frames that share the same
    /// reference frame.
    /// </summary>
    public static bool TryTransformPoint(
        CartesianCoordinate3 point,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CartesianCoordinate3 result) {
        return TryTransformPoint(point, source, target, out result, out _);
    }

    /// <summary>
    /// Attempts to transform a point between two frames and reports the
    /// terminating numerical condition.
    /// </summary>
    public static bool TryTransformPoint(
        CartesianCoordinate3 point,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        result = default;
        if (!IsFinite(point) ||
            !IsFrameFinite(source) ||
            !IsFrameFinite(target)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FramePointTransform,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!source.TryPointToReferenceCore(point, out var reference)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FramePointTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!target.TryPointFromReferenceCore(reference, out result)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FramePointTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.FramePointTransform,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Rotates a direction between two frames that share the same reference
    /// frame.
    /// </summary>
    public static Vector3<double> TransformDirection(
        Vector3<double> direction,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        if (!TryTransformDirection(direction, source, target, out var result)) {
            throw new ArithmeticException(
                "The transformed direction is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to rotate a direction between two frames that share the same
    /// reference frame.
    /// </summary>
    public static bool TryTransformDirection(
        Vector3<double> direction,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out Vector3<double> result) {
        return TryTransformDirection(direction, source, target, out result, out _);
    }

    /// <summary>
    /// Attempts to rotate a direction between two frames and reports the
    /// terminating numerical condition.
    /// </summary>
    public static bool TryTransformDirection(
        Vector3<double> direction,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        result = default;
        if (!CartesianVector3Math.IsFinite(direction) ||
            !IsFrameFinite(source) ||
            !IsFrameFinite(target)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameDirectionTransform,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!source.TryDirectionToReferenceCore(direction, out var reference)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameDirectionTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!target.TryDirectionFromReferenceCore(reference, out result)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameDirectionTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.FrameDirectionTransform,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Transforms a frame pose between two frames that share the same reference
    /// frame.
    /// </summary>
    /// <remarks>
    /// This operation is also the composition primitive for hierarchy. If
    /// <paramref name="frame"/> is a surface frame expressed in an element frame,
    /// transforming it from that element frame to the global frame yields the
    /// surface pose in global coordinates.
    /// </remarks>
    public static CoordinateFrame3 TransformFrame(
        CoordinateFrame3 frame,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        if (!TryTransformFrame(frame, source, target, out var result)) {
            throw new ArithmeticException(
                "The transformed frame is not finite.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to transform a frame pose between two frames that share the same
    /// reference frame.
    /// </summary>
    public static bool TryTransformFrame(
        CoordinateFrame3 frame,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CoordinateFrame3 result) {
        return TryTransformFrame(frame, source, target, out result, out _);
    }

    /// <summary>
    /// Attempts to transform a frame pose and reports the terminating numerical
    /// condition.
    /// </summary>
    public static bool TryTransformFrame(
        CoordinateFrame3 frame,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CoordinateFrame3 result,
        out CoordinateOperationReport report) {
        result = default;
        if (!IsFrameFinite(frame) ||
            !IsFrameFinite(source) ||
            !IsFrameFinite(target)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameTransform,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        if (!source.TryPointToReferenceCore(frame.Origin, out var referenceOrigin) ||
            !target.TryPointFromReferenceCore(referenceOrigin, out var origin)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!source.TryDirectionToReferenceCore(frame.XAxis, out var referenceX) ||
            !target.TryDirectionFromReferenceCore(referenceX, out var xAxis)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!source.TryDirectionToReferenceCore(frame.YAxis, out var referenceY) ||
            !target.TryDirectionFromReferenceCore(referenceY, out var yAxis)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameTransform,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!TryCreate(
            origin,
            xAxis,
            yAxis,
            frame.Handedness,
            out result,
            out var construction)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.FrameTransform,
                construction.Status);
            return false;
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.FrameTransform,
            CoordinateOperationStatus.Succeeded);
        return true;
    }

    private static bool TryOrthogonalize(
        Vector3<double> candidate,
        Vector3<double> unitAxis,
        out Vector3<double> result) {
        result = default;
        if (!CartesianVector3Math.TryNormalize(candidate, out var unitCandidate)) {
            return false;
        }

        var projection = Vector3<double>.Dot(unitCandidate, unitAxis);
        var orthogonal = unitCandidate - (unitAxis * projection);
        return CartesianVector3Math.TryNormalize(orthogonal, out result);
    }

    private static bool TryCreatePoint(
        double x,
        double y,
        double z,
        out CartesianCoordinate3 result) {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) {
            result = default;
            return false;
        }

        result = new CartesianCoordinate3(x, y, z);
        return true;
    }

    private static bool IsFinite(CartesianCoordinate3 point) {
        return double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);
    }

    private static bool IsFrameFinite(CoordinateFrame3 frame) {
        return IsHandednessValid(frame.Handedness) &&
            IsFinite(frame.Origin) &&
            CartesianVector3Math.IsFinite(frame.XAxis) &&
            CartesianVector3Math.IsFinite(frame.YAxis) &&
            CartesianVector3Math.IsFinite(frame.ZAxis);
    }

    private static bool IsHandednessValid(CoordinateHandedness handedness) {
        return handedness is
            CoordinateHandedness.RightHanded or
            CoordinateHandedness.LeftHanded;
    }
}
