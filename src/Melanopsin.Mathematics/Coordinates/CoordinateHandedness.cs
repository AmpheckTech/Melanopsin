// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Describes the orientation parity of an ordered three-dimensional basis.
/// </summary>
/// <remarks>
/// Handedness is a property of a coordinate frame or basis, not of a matrix
/// value. The same convention is relevant to graphics, geodesy,
/// electromagnetics, optics, and robotics. Domain-specific APIs should accept
/// it explicitly when the interpretation of a transform depends on it.
/// </remarks>
public enum CoordinateHandedness {
    /// <summary>
    /// Positive X cross positive Y points toward positive Z.
    /// </summary>
    RightHanded,

    /// <summary>
    /// Positive X cross positive Y points away from positive Z.
    /// </summary>
    LeftHanded,
}
