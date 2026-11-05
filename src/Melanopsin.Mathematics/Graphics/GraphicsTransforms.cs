using Melanopsin.Mathematics.Coordinates;

namespace Melanopsin.Mathematics.Graphics;

/// <summary>
/// Creates graphics-specific view and projection matrices from the
/// convention-neutral matrix types.
/// </summary>
/// <remarks>
/// Camera-space and clip-space conventions live here rather than on
/// <see cref="Matrix4x4"/> so that the core algebra remains domain-neutral.
/// This is a mathematical transform factory only; it intentionally does not
/// model cameras, scene graphs, rendering resources, or interaction controllers.
/// </remarks>
public static class GraphicsTransforms {
    /// <summary>
    /// Creates a view matrix for the specified coordinate handedness.
    /// </summary>
    /// <remarks>
    /// In a right-handed camera space, the camera looks toward negative Z. In a
    /// left-handed camera space, it looks toward positive Z.
    /// </remarks>
    public static Matrix4x4 CreateLookAt(
        Vector3 position,
        Vector3 target,
        Vector3 up,
        CoordinateHandedness handedness) {
        if (!position.IsFinite || !target.IsFinite || !up.IsFinite) {
            throw new ArgumentException("Look-at vectors must be finite.");
        }

        if (!(target - position).TryNormalize(out var forward)) {
            throw new ArgumentException(
                "The look-at position and target must be different.",
                nameof(target));
        }

        var viewZ = handedness switch {
            CoordinateHandedness.RightHanded => -forward,
            CoordinateHandedness.LeftHanded => forward,
            _ => throw new ArgumentOutOfRangeException(nameof(handedness)),
        };
        if (!Vector3.Cross(up, viewZ).TryNormalize(out var right)) {
            throw new ArgumentException(
                "The up vector must not be parallel to the view direction.",
                nameof(up));
        }

        var correctedUp = Vector3.Cross(viewZ, right);
        return new Matrix4x4(
            right.X, right.Y, right.Z, -Vector3.Dot(right, position),
            correctedUp.X, correctedUp.Y, correctedUp.Z, -Vector3.Dot(correctedUp, position),
            viewZ.X, viewZ.Y, viewZ.Z, -Vector3.Dot(viewZ, position),
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreatePerspectiveFieldOfView(
        float fieldOfViewRadians,
        float aspectRatio,
        float nearPlane,
        float farPlane,
        CoordinateHandedness handedness) {
        if (!float.IsFinite(fieldOfViewRadians) ||
            fieldOfViewRadians <= 0 ||
            fieldOfViewRadians >= MathF.PI) {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewRadians));
        }

        if (!float.IsFinite(aspectRatio) || aspectRatio <= 0) {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        }

        ValidateDepthRange(nearPlane, farPlane);
        var tangent = MathF.Tan(fieldOfViewRadians / 2);
        var verticalScale = 1 / tangent;
        var horizontalScale = verticalScale / aspectRatio;
        var depthRange = handedness switch {
            CoordinateHandedness.RightHanded => nearPlane - farPlane,
            CoordinateHandedness.LeftHanded => farPlane - nearPlane,
            _ => throw new ArgumentOutOfRangeException(nameof(handedness)),
        };
        var depthScale = farPlane / depthRange;
        var depthTranslation = handedness == CoordinateHandedness.RightHanded
            ? (nearPlane * farPlane) / depthRange
            : -(nearPlane * farPlane) / depthRange;
        var wScale = handedness == CoordinateHandedness.RightHanded ? -1 : 1;
        return new Matrix4x4(
            horizontalScale, 0, 0, 0,
            0, verticalScale, 0, 0,
            0, 0, depthScale, depthTranslation,
            0, 0, wScale, 0);
    }

    public static Matrix4x4 CreateOrthographic(
        float width,
        float height,
        float nearPlane,
        float farPlane,
        CoordinateHandedness handedness) {
        if (!float.IsFinite(width) || width <= 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!float.IsFinite(height) || height <= 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ValidateDepthRange(nearPlane, farPlane);
        var depthRange = handedness switch {
            CoordinateHandedness.RightHanded => nearPlane - farPlane,
            CoordinateHandedness.LeftHanded => farPlane - nearPlane,
            _ => throw new ArgumentOutOfRangeException(nameof(handedness)),
        };
        var depthScale = 1 / depthRange;
        var depthTranslation = handedness == CoordinateHandedness.RightHanded
            ? nearPlane / depthRange
            : -nearPlane / depthRange;
        return new Matrix4x4(
            2 / width, 0, 0, 0,
            0, 2 / height, 0, 0,
            0, 0, depthScale, depthTranslation,
            0, 0, 0, 1);
    }

    private static void ValidateDepthRange(float nearPlane, float farPlane) {
        if (!float.IsFinite(nearPlane) || nearPlane <= 0) {
            throw new ArgumentOutOfRangeException(nameof(nearPlane));
        }

        if (!float.IsFinite(farPlane) || farPlane <= nearPlane) {
            throw new ArgumentOutOfRangeException(nameof(farPlane));
        }
    }
}
