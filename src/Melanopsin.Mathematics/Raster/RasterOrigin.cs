// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Raster;

/// <summary>
/// Selects the origin and positive vertical direction of a raster coordinate
/// system. Horizontal coordinates always increase to the right.
/// </summary>
public enum RasterOrigin {
    /// <summary>
    /// The origin is the top-left corner and positive Y points downward.
    /// </summary>
    TopLeft,

    /// <summary>
    /// The origin is the bottom-left corner and positive Y points upward.
    /// </summary>
    BottomLeft,
}
