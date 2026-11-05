// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Produces a damped spring-like curve with a small overshoot.
/// </summary>
public sealed record SpringEasing : Easing {
    private static readonly float EndpointOffset = EvaluateRaw(1) - 1;

    protected override float EvaluateNormalized(float progress) {
        // Remove the small residual at the endpoint without introducing a
        // value discontinuity between the last sub-endpoint sample and 1.
        var correction = progress * progress * (3 - (2 * progress));
        return Math.Clamp(
            EvaluateRaw(progress) - (EndpointOffset * correction),
            0,
            1.08f);
    }

    private static float EvaluateRaw(float progress) {
        return 1 - (MathF.Exp(-6 * progress) * MathF.Cos(10 * progress));
    }
}
