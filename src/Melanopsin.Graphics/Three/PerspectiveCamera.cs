namespace Melanopsin.Graphics.Three;

/// <summary>
/// A right-handed perspective camera.
/// </summary>
public sealed class PerspectiveCamera {
    private const float MaximumPitch = 89;
    private float _fieldOfViewDegrees = 60;
    private float _nearPlane = 0.1f;
    private float _farPlane = 1000;

    public PerspectiveCamera(
        Vector3 position,
        Vector3 target,
        Vector3? up = null) {
        if (!IsFinite(position) || !IsFinite(target)) {
            throw new ArgumentException("Camera position and target must be finite.");
        }

        if (Vector3.DistanceSquared(position, target) <= float.Epsilon) {
            throw new ArgumentException(
                "Camera position and target must be different.");
        }

        Position = position;
        Target = target;
        Up = up ?? Vector3.UnitY;
    }

    public Vector3 Position { get; set; }

    public Vector3 Target { get; set; }

    public Vector3 Up { get; set; }

    public float FieldOfViewDegrees {
        get => _fieldOfViewDegrees;
        set {
            if (!float.IsFinite(value) || value <= 0 || value >= 180) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _fieldOfViewDegrees = value;
        }
    }

    public float NearPlane {
        get => _nearPlane;
        set {
            if (!float.IsFinite(value) || value <= 0) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value >= _farPlane) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _nearPlane = value;
        }
    }

    public float FarPlane {
        get => _farPlane;
        set {
            if (!float.IsFinite(value) || value <= _nearPlane) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _farPlane = value;
        }
    }

    /// <summary>
    /// Orbits the camera around its current target. Angles are expressed in
    /// degrees and pitch is clamped before reaching either pole.
    /// </summary>
    public void Orbit(float yawDegrees, float pitchDegrees) {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees)) {
            throw new ArgumentException("Orbit angles must be finite.");
        }

        var offset = Position - Target;
        var distance = offset.Length;
        if (distance <= float.Epsilon) {
            return;
        }

        var yaw = MathF.Atan2(offset.X, offset.Z) +
            ScalarMath.DegreesToRadians(yawDegrees);
        var pitch = Math.Clamp(
            MathF.Asin(Math.Clamp(offset.Y / distance, -1, 1)) +
                ScalarMath.DegreesToRadians(pitchDegrees),
            ScalarMath.DegreesToRadians(-MaximumPitch),
            ScalarMath.DegreesToRadians(MaximumPitch));
        var horizontalDistance = MathF.Cos(pitch) * distance;
        Position = Target + new Vector3(
            MathF.Sin(yaw) * horizontalDistance,
            MathF.Sin(pitch) * distance,
            MathF.Cos(yaw) * horizontalDistance);
    }

    public Matrix4x4 CreateViewMatrix() {
        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    public Matrix4x4 CreateProjectionMatrix(float aspectRatio) {
        if (!float.IsFinite(aspectRatio) || aspectRatio <= 0) {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        }

        return Matrix4x4.CreatePerspectiveFieldOfView(
            ScalarMath.DegreesToRadians(FieldOfViewDegrees),
            aspectRatio,
            NearPlane,
            FarPlane);
    }

    private static bool IsFinite(Vector3 value) {
        return float.IsFinite(value.X) &&
            float.IsFinite(value.Y) &&
            float.IsFinite(value.Z);
    }

}
