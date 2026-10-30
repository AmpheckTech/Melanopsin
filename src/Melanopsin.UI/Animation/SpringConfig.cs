namespace Melanopsin.UI.Animation;

/// <summary>
/// Parameters for a damped harmonic oscillator using coherent position unit
/// <c>U</c> and mass unit <c>M</c>.
/// </summary>
/// <remarks>
/// Stiffness is expressed in <c>M/s²</c>, damping in <c>M/s</c>, and mass in
/// <c>M</c>. No unit conversion is performed. For example, when position uses
/// meters and mass uses kilograms, stiffness is <c>kg/s²</c> and damping is
/// <c>kg/s</c>.
/// </remarks>
public readonly record struct SpringConfig {
    /// <summary>
    /// Creates a configuration with the documented defaults:
    /// <c>stiffness: 160</c>, <c>damping: 24</c>, <c>mass: 1</c>,
    /// <c>restDistance: 0.01</c>, and <c>restVelocity: 0.01</c>.
    /// </summary>
    public SpringConfig()
        : this(160f, 24f, 1f, 0.01f, 0.01f) {
    }

    /// <summary>
    /// Creates a spring configuration.
    /// </summary>
    /// <param name="stiffness">Spring constant in mass units per second squared.</param>
    /// <param name="damping">Damping coefficient in mass units per second.</param>
    /// <param name="mass">Mass in caller-defined mass units.</param>
    /// <param name="restDistance">Rest distance threshold in position units.</param>
    /// <param name="restVelocity">Rest velocity threshold in position units per second.</param>
    public SpringConfig(
        float stiffness = 160f,
        float damping = 24f,
        float mass = 1f,
        float restDistance = 0.01f,
        float restVelocity = 0.01f) {
        if (!float.IsFinite(stiffness) ||
            !float.IsFinite(damping) ||
            !float.IsFinite(mass) ||
            !float.IsFinite(restDistance) ||
            !float.IsFinite(restVelocity)) {
            throw new ArgumentOutOfRangeException(
                nameof(stiffness),
                "Spring configuration values must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stiffness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(damping);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mass);
        ArgumentOutOfRangeException.ThrowIfNegative(restDistance);
        ArgumentOutOfRangeException.ThrowIfNegative(restVelocity);

        Stiffness = stiffness;
        Damping = damping;
        Mass = mass;
        RestDistance = restDistance;
        RestVelocity = restVelocity;
    }

    /// <summary>
    /// Gets the spring constant in mass units per second squared. When position
    /// uses meters and mass uses kilograms, this is equivalent to <c>N/m</c>.
    /// </summary>
    public float Stiffness { get; }

    /// <summary>
    /// Gets the damping coefficient in mass units per second.
    /// </summary>
    public float Damping { get; }

    /// <summary>
    /// Gets the mass in caller-defined mass units.
    /// </summary>
    public float Mass { get; }

    /// <summary>
    /// Gets the maximum absolute distance from the target at which the spring
    /// may be considered at rest, in the caller's position unit.
    /// </summary>
    public float RestDistance { get; }

    /// <summary>
    /// Gets the maximum absolute velocity at which the spring may be considered
    /// at rest, in position units per second.
    /// </summary>
    public float RestVelocity { get; }

    internal void Validate() {
        if (!float.IsFinite(Stiffness) || Stiffness <= 0 ||
            !float.IsFinite(Damping) || Damping <= 0 ||
            !float.IsFinite(Mass) || Mass <= 0 ||
            !float.IsFinite(RestDistance) || RestDistance < 0 ||
            !float.IsFinite(RestVelocity) || RestVelocity < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(Stiffness),
                "Spring stiffness, damping, and mass must be positive; rest thresholds must be non-negative; all values must be finite.");
        }
    }
}
