// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// Standard Jones and Mueller polarization elements in the horizontal/vertical
/// transverse basis used by <see cref="JonesVector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Angles are measured counterclockwise from the positive X axis when positive
/// Y forms a right-handed transverse basis. The Stokes convention is inherited
/// from <see cref="JonesVector.ToStokes"/>:
/// <c>Q=|X|^2-|Y|^2</c>, <c>U=2 Re(XY*)</c>, and <c>V=-2 Im(XY*)</c>.
/// Therefore <see cref="JonesVector"/> with equal in-phase components is +45°
/// linear, and <c>Y=iX</c> is right-circular with positive V.
/// </para>
/// <para>
/// These presets are algebraic ideal elements. They do not model wavelength,
/// material dispersion, surface reflection, diattenuation, or physical
/// realizability beyond the stated construction. Retarder phase is the
/// phase delay of the slow axis relative to the fast axis. Global phase is
/// chosen so the fast-axis component has unit Jones amplitude.
/// </para>
/// </remarks>
public static class PolarizationOptics {
    /// <summary>Gets unit-intensity horizontal linear polarization.</summary>
    public static JonesVector HorizontalLinear => new(Complex.One, Complex.Zero);

    /// <summary>Gets unit-intensity vertical linear polarization.</summary>
    public static JonesVector VerticalLinear => new(Complex.Zero, Complex.One);

    /// <summary>Gets unit-intensity +45° linear polarization.</summary>
    public static JonesVector DiagonalLinear => new(
        Constants.InverseSqrt2,
        Constants.InverseSqrt2);

    /// <summary>Gets unit-intensity -45° linear polarization.</summary>
    public static JonesVector AntiDiagonalLinear => new(
        Constants.InverseSqrt2,
        -Constants.InverseSqrt2);

    /// <summary>
    /// Gets unit-intensity right-circular polarization, for which Stokes V is
    /// positive under this library's convention.
    /// </summary>
    public static JonesVector RightCircular => new(
        Constants.InverseSqrt2,
        Complex.ImaginaryOne * Constants.InverseSqrt2);

    /// <summary>
    /// Gets unit-intensity left-circular polarization, for which Stokes V is
    /// negative under this library's convention.
    /// </summary>
    public static JonesVector LeftCircular => new(
        Constants.InverseSqrt2,
        -Complex.ImaginaryOne * Constants.InverseSqrt2);

    /// <summary>
    /// Creates an ideal linear polarizer whose transmission axis is at
    /// <paramref name="axisAngleRadians"/> from positive X.
    /// </summary>
    public static JonesMatrix LinearPolarizer(double axisAngleRadians) {
        ValidateAngle(axisAngleRadians, nameof(axisAngleRadians));
        var cosine = Math.Cos(axisAngleRadians);
        var sine = Math.Sin(axisAngleRadians);
        var product = cosine * sine;
        return new JonesMatrix(
            cosine * cosine,
            product,
            product,
            sine * sine);
    }

    /// <summary>
    /// Creates a lossless ideal Jones retarder with the supplied fast-axis angle
    /// and slow-minus-fast phase delay.
    /// </summary>
    public static JonesMatrix Retarder(
        double retardanceRadians,
        double fastAxisAngleRadians) {
        ValidateAngle(retardanceRadians, nameof(retardanceRadians));
        ValidateAngle(fastAxisAngleRadians, nameof(fastAxisAngleRadians));
        var cosine = Math.Cos(fastAxisAngleRadians);
        var sine = Math.Sin(fastAxisAngleRadians);
        var phase = Complex.FromPolarCoordinates(1, retardanceRadians);
        var offDiagonal = cosine * sine * (1 - phase);
        return new JonesMatrix(
            (cosine * cosine) + (phase * sine * sine),
            offDiagonal,
            offDiagonal,
            (sine * sine) + (phase * cosine * cosine));
    }

    /// <summary>Creates an ideal half-wave plate.</summary>
    public static JonesMatrix HalfWavePlate(double fastAxisAngleRadians) {
        return Retarder(Constants.Pi, fastAxisAngleRadians);
    }

    /// <summary>Creates an ideal quarter-wave plate.</summary>
    public static JonesMatrix QuarterWavePlate(double fastAxisAngleRadians) {
        return Retarder(Constants.PiOver2, fastAxisAngleRadians);
    }

    /// <summary>Creates a polarization-axis rotator that maps X toward the specified angle.</summary>
    public static JonesMatrix Rotator(double rotationAngleRadians) {
        ValidateAngle(rotationAngleRadians, nameof(rotationAngleRadians));
        var cosine = Math.Cos(rotationAngleRadians);
        var sine = Math.Sin(rotationAngleRadians);
        return new JonesMatrix(
            cosine,
            -sine,
            sine,
            cosine);
    }

    /// <summary>
    /// Creates the Mueller matrix of an ideal linear polarizer whose
    /// transmission axis is at <paramref name="axisAngleRadians"/> from
    /// positive X.
    /// </summary>
    public static MuellerMatrix LinearPolarizerMueller(double axisAngleRadians) {
        ValidateAngle(axisAngleRadians, nameof(axisAngleRadians));
        var cosine = Math.Cos(2 * axisAngleRadians);
        var sine = Math.Sin(2 * axisAngleRadians);
        var c2 = cosine * cosine;
        var s2 = sine * sine;
        var cs = cosine * sine;
        return new MuellerMatrix(
            0.5, 0.5 * cosine, 0.5 * sine, 0,
            0.5 * cosine, 0.5 * c2, 0.5 * cs, 0,
            0.5 * sine, 0.5 * cs, 0.5 * s2, 0,
            0, 0, 0, 0);
    }

    /// <summary>
    /// Creates the Mueller matrix of a lossless ideal retarder with the supplied
    /// fast-axis angle and slow-minus-fast phase delay.
    /// </summary>
    /// <remarks>
    /// The matrix is the retarding-element form of the Stokes-Mueller calculus
    /// in Born and Wolf, <i>Principles of Optics</i>, 7th ed. (1999), ch. 1,
    /// adapted to the V sign convention documented by
    /// <see cref="JonesVector"/>.
    /// </remarks>
    public static MuellerMatrix RetarderMueller(
        double retardanceRadians,
        double fastAxisAngleRadians) {
        ValidateAngle(retardanceRadians, nameof(retardanceRadians));
        ValidateAngle(fastAxisAngleRadians, nameof(fastAxisAngleRadians));
        var cosine = Math.Cos(2 * fastAxisAngleRadians);
        var sine = Math.Sin(2 * fastAxisAngleRadians);
        var phaseCosine = Math.Cos(retardanceRadians);
        var phaseSine = Math.Sin(retardanceRadians);
        var c2 = cosine * cosine;
        var s2 = sine * sine;
        var cs = cosine * sine;
        var coupling = cs * (1 - phaseCosine);
        return new MuellerMatrix(
            1, 0, 0, 0,
            0, c2 + (s2 * phaseCosine), coupling, sine * phaseSine,
            0, coupling, s2 + (c2 * phaseCosine), -cosine * phaseSine,
            0, -sine * phaseSine, cosine * phaseSine, phaseCosine);
    }

    private static void ValidateAngle(double angle, string parameterName) {
        if (!double.IsFinite(angle)) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
