// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Selects the sample-quantile interpolation convention. The definitions
/// correspond to the nine Hyndman-Fan sample-quantile estimators.
/// </summary>
/// <remarks>
/// Reference: Hyndman and Fan (1996),
/// <see href="https://doi.org/10.2307/2684934">doi:10.2307/2684934</see>.
/// </remarks>
public enum QuantileDefinition {
    /// <summary>
    /// Invalid default sentinel. Callers must select an estimator explicitly.
    /// </summary>
    None = 0,

    /// <summary>
    /// Inverse empirical distribution: the first observation whose cumulative
    /// plotting position is at least <c>probability</c>.
    /// </summary>
    InverseEmpirical = 1,

    /// <summary>
    /// Inverse empirical distribution with averaging across a discontinuity.
    /// </summary>
    AveragedInverseEmpirical = 2,

    /// <summary>
    /// Nearest order statistic, with exact ties rounded to an even rank.
    /// </summary>
    NearestEven = 3,

    /// <summary>
    /// Linear interpolation at plotting positions <c>k / n</c>.
    /// </summary>
    LinearWithEndpoints = 4,

    /// <summary>
    /// Piecewise linear interpolation at plotting positions
    /// <c>(k - 1/2) / n</c>.
    /// </summary>
    Midpoint = 5,

    /// <summary>
    /// Linear interpolation at plotting positions <c>k / (n + 1)</c>.
    /// </summary>
    Weibull = 6,

    /// <summary>
    /// Linear interpolation at plotting positions <c>(k - 1) / (n - 1)</c>.
    /// This is the library default.
    /// </summary>
    Linear = 7,

    /// <summary>
    /// Linear interpolation at median-unbiased plotting positions
    /// <c>(k - 1/3) / (n + 1/3)</c>.
    /// </summary>
    MedianUnbiased = 8,

    /// <summary>
    /// Linear interpolation at approximately normal-unbiased plotting
    /// positions <c>(k - 3/8) / (n + 1/4)</c>.
    /// </summary>
    NormalUnbiased = 9,
}
