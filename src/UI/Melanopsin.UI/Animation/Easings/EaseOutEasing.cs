// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Starts quickly and decelerates toward the endpoint.
/// </summary>
public sealed record EaseOutEasing : Easing {
    protected override float EvaluateNormalized(float progress) {
        return 1 - MathF.Pow(1 - progress, 3);
    }
}
