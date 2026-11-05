// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Animation;

/// <summary>
/// A mounted node that advances an animation on each UI frame.
/// </summary>
public interface IAnimationParticipant {
    /// <summary>
    /// Advances animation state by <paramref name="elapsedSeconds"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> while another frame is needed; implementations that
    /// change their presentation must invalidate themselves.
    /// </returns>
    bool AdvanceAnimation(float elapsedSeconds);
}
