namespace Melanopsin.UI.Animation;

/// <summary>
/// Parameters for a damped harmonic oscillator, in caller-defined position and
/// mass units. No unit conversion is performed.
/// </summary>
public readonly record struct SpringConfig {
    public SpringConfig()
        : this(160f, 24f, 1f, 0.01f, 0.01f) {
    }

    public SpringConfig(
        float stiffness = 160f,
        float damping = 24f,
        float mass = 1f,
        float restDistance = 0.01f,
        float restVelocity = 0.01f) {
        Validate(stiffness, damping, mass, restDistance, restVelocity);
        Stiffness = stiffness;
        Damping = damping;
        Mass = mass;
        RestDistance = restDistance;
        RestVelocity = restVelocity;
    }

    /// <summary>Spring constant, in mass units per second squared.</summary>
    public float Stiffness { get; }

    /// <summary>Damping coefficient, in mass units per second.</summary>
    public float Damping { get; }

    /// <summary>Mass, in caller-defined mass units.</summary>
    public float Mass { get; }

    /// <summary>
    /// Distance from the target below which the spring counts as at rest, in
    /// position units.
    /// </summary>
    public float RestDistance { get; }

    /// <summary>
    /// Velocity below which the spring counts as at rest, in position units per
    /// second.
    /// </summary>
    public float RestVelocity { get; }

    /// <summary>
    /// Re-checks this value for instances that skipped the constructor, such as
    /// <c>default(SpringConfig)</c>.
    /// </summary>
    internal void Validate() =>
        Validate(Stiffness, Damping, Mass, RestDistance, RestVelocity);

    private static void Validate(
        float stiffness,
        float damping,
        float mass,
        float restDistance,
        float restVelocity) {
        ThrowIfNotFinite(stiffness, nameof(stiffness));
        ThrowIfNotFinite(damping, nameof(damping));
        ThrowIfNotFinite(mass, nameof(mass));
        ThrowIfNotFinite(restDistance, nameof(restDistance));
        ThrowIfNotFinite(restVelocity, nameof(restVelocity));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stiffness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(damping);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mass);
        ArgumentOutOfRangeException.ThrowIfNegative(restDistance);
        ArgumentOutOfRangeException.ThrowIfNegative(restVelocity);

        static void ThrowIfNotFinite(float value, string name) {
            if (!float.IsFinite(value)) {
                throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    "Spring configuration values must be finite.");
            }
        }
    }
}
