// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Quantizes progress into a fixed number of discrete steps.
/// </summary>
public sealed record StepEasing : Easing {
    public StepEasing(
        int steps,
        StepPosition position = StepPosition.JumpEnd) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        if (position is not (
            StepPosition.JumpStart or
            StepPosition.JumpEnd or
            StepPosition.JumpNone or
            StepPosition.JumpBoth)) {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "The step position must be a defined value.");
        }

        if (position == StepPosition.JumpNone && steps < 2) {
            throw new ArgumentOutOfRangeException(
                nameof(steps),
                steps,
                "JumpNone requires at least two steps.");
        }

        Steps = steps;
        Position = position;
    }

    public int Steps { get; }

    public StepPosition Position { get; }

    protected override float EvaluateNormalized(float progress) {
        return Position switch {
            StepPosition.JumpStart => MathF.Ceiling(progress * Steps) / Steps,
            StepPosition.JumpEnd => MathF.Floor(progress * Steps) / Steps,
            StepPosition.JumpNone => MathF.Floor(progress * Steps) / (Steps - 1),
            StepPosition.JumpBoth => MathF.Floor((progress * Steps) + 1) / (Steps + 1),
            _ => throw new InvalidOperationException("Unknown step position."),
        };
    }
}
