namespace Melanopsin.UI.Animation;

public static class SpringSolver {
    /// <summary>
    /// Advances a damped spring by one semi-implicit Euler step.
    /// </summary>
    public static void Step(
        ref float position,
        ref float velocity,
        float target,
        float deltaSeconds,
        SpringConfig config) {
        if (!float.IsFinite(position) ||
            !float.IsFinite(velocity) ||
            !float.IsFinite(target)) {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Spring position, velocity, and target must be finite.");
        }

        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Spring delta must be finite and non-negative.");
        }

        config.Validate();
        if (deltaSeconds == 0) {
            return;
        }

        var acceleration =
            (((target - position) * config.Stiffness) -
             (velocity * config.Damping)) / config.Mass;
        velocity += acceleration * deltaSeconds;
        position += velocity * deltaSeconds;
    }
}
