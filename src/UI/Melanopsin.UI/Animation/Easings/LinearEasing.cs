// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Maps normalized time linearly to progress.
/// </summary>
public sealed record LinearEasing : Easing {
    protected override float EvaluateNormalized(float progress) {
        return progress;
    }
}
