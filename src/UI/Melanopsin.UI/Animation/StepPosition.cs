// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// Controls where values jump in a <see cref="StepEasing"/>.
/// </summary>
public enum StepPosition {
    /// <summary>
    /// Jumps at the start of each interval. The initial value after zero is
    /// <c>1 / steps</c>.
    /// </summary>
    JumpStart,

    /// <summary>
    /// Jumps at the end of each interval. This is the default used by
    /// <c>steps(n)</c> and starts at zero.
    /// </summary>
    JumpEnd,

    /// <summary>
    /// Omits jumps at both endpoints. Requires at least two steps.
    /// </summary>
    JumpNone,

    /// <summary>
    /// Jumps at both endpoints.
    /// </summary>
    JumpBoth,
}
