// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Motion;

public readonly record struct UiAngle(float Degrees, AngleUnit Unit = AngleUnit.Degrees) {
    public static UiAngle Deg(float degrees) {
        if (!float.IsFinite(degrees)) {
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, null);
        }

        return new UiAngle(degrees, AngleUnit.Degrees);
    }

    public static UiAngle Rad(float radians) {
        if (!float.IsFinite(radians)) {
            throw new ArgumentOutOfRangeException(nameof(radians), radians, null);
        }

        return new UiAngle(radians * (180f / MathF.PI), AngleUnit.Radians);
    }

    public static UiAngle Turn(float turns) {
        if (!float.IsFinite(turns)) {
            throw new ArgumentOutOfRangeException(nameof(turns), turns, null);
        }

        return new UiAngle(turns * 360f, AngleUnit.Turns);
    }
}
