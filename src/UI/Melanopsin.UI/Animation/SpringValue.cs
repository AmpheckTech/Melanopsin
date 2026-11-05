// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// A scalar spring state that can be advanced independently of a UI frame loop.
/// </summary>
public sealed class SpringValue {
    private float _velocity;

    public SpringValue(
        float initialValue = 0,
        float targetValue = 0,
        SpringConfig? config = null) {
        if (!float.IsFinite(initialValue) || !float.IsFinite(targetValue)) {
            throw new ArgumentOutOfRangeException(
                nameof(initialValue),
                "Spring values must be finite.");
        }

        Position = initialValue;
        Target = targetValue;
        Config = config ?? new SpringConfig();
        Config.Validate();
    }

    public float Position { get; private set; }

    public float Velocity {
        get => _velocity;
        set {
            if (!float.IsFinite(value)) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Spring velocity must be finite.");
            }

            _velocity = value;
        }
    }

    public float Target { get; private set; }

    public SpringConfig Config { get; }

    public bool IsAtRest =>
        Math.Abs(Position - Target) <= Config.RestDistance &&
        Math.Abs(Velocity) <= Config.RestVelocity;

    public void SetTarget(float target) {
        if (!float.IsFinite(target)) {
            throw new ArgumentOutOfRangeException(
                nameof(target),
                target,
                "Spring target must be finite.");
        }

        Target = target;
    }

    /// <summary>
    /// Integrates the spring by a number of seconds.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a step was performed, <see langword="false"/>
    /// when the spring was already at rest.
    /// </returns>
    public bool Advance(float deltaSeconds) {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Spring delta must be finite and non-negative.");
        }

        if (IsAtRest) {
            Position = Target;
            Velocity = 0;
            return false;
        }

        var position = Position;
        var velocity = Velocity;
        SpringSolver.Step(
            ref position,
            ref velocity,
            Target,
            deltaSeconds,
            Config);
        Position = position;
        Velocity = velocity;
        if (IsAtRest) {
            Position = Target;
            Velocity = 0;
        }

        return true;
    }
}
