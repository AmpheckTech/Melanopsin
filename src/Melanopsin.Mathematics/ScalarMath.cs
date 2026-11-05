// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Scalar angle conversions, interpolation, range helpers, and approximate
/// comparisons. The implementation is split across partial files.
/// </summary>
public static partial class ScalarMath {
    /// <summary>
    /// The difference between 1 and the next representable double-precision
    /// value. This is machine epsilon, not the smallest positive
    /// <see cref="double"/> value.
    /// </summary>
    public const double MachineEpsilon = 2.2204460492503130808472633361816e-16;

    /// <summary>
    /// The difference between 1 and the next representable single-precision
    /// value. This is machine epsilon, not <see cref="float.Epsilon"/>.
    /// </summary>
    public const float MachineEpsilonSingle = 1.1920928955078125e-7f;

    /// <summary>
    /// The default absolute tolerance for double-precision approximate
    /// comparisons. This is 1e-9, not machine epsilon, and it is not a
    /// singularity threshold. Approximate comparisons include the boundary:
    /// |value| &lt;= tolerance.
    /// </summary>
    public const double DefaultTolerance = 0.000000001;

    /// <summary>
    /// The default absolute tolerance for single-precision approximate
    /// comparisons. This is 1e-6f. It is intentionally looser than
    /// <see cref="DefaultTolerance"/> to account for the lower precision of
    /// <see cref="float"/>.
    /// </summary>
    public const float DefaultSingleTolerance = 0.000001f;

    public static float DegreesToRadians(float degrees) {
        return degrees * Constants.RadiansPerDegreeF;
    }

    public static double DegreesToRadians(double degrees) {
        return degrees * Constants.RadiansPerDegree;
    }

    public static float RadiansToDegrees(float radians) {
        return radians * Constants.DegreesPerRadianF;
    }

    public static double RadiansToDegrees(double radians) {
        return radians * Constants.DegreesPerRadian;
    }
}
