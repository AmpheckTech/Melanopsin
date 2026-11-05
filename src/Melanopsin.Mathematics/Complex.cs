// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;
using BclComplex = System.Numerics.Complex;

namespace Melanopsin.Mathematics;

/// <summary>
/// A double-precision complex number with components
/// <see cref="Real"/> + <see cref="Imaginary"/>i.
/// </summary>
/// <remarks>
/// This is the canonical complex scalar for the library. Conversion to and
/// from <see cref="BclComplex"/> is explicit at the interoperability boundary.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Complex :
    IAdditionOperators<Complex, Complex, Complex>,
    ISubtractionOperators<Complex, Complex, Complex>,
    IUnaryNegationOperators<Complex, Complex>,
    IUnaryPlusOperators<Complex, Complex>,
    IMultiplyOperators<Complex, Complex, Complex>,
    IDivisionOperators<Complex, Complex, Complex>,
    IAdditiveIdentity<Complex, Complex>,
    IMultiplicativeIdentity<Complex, Complex>,
    IEqualityOperators<Complex, Complex, bool> {
    public Complex(double real, double imaginary) {
        Real = real;
        Imaginary = imaginary;
    }

    public static Complex Zero => default;

    public static Complex One => new(1, 0);

    public static Complex ImaginaryOne => new(0, 1);

    public static Complex AdditiveIdentity => Zero;

    public static Complex MultiplicativeIdentity => One;

    public double Real { get; }

    public double Imaginary { get; }

    public double Magnitude => Hypot(Real, Imaginary);

    public double MagnitudeSquared => (Real * Real) + (Imaginary * Imaginary);

    public double Phase => Math.Atan2(Imaginary, Real);

    public bool IsFinite =>
        double.IsFinite(Real) && double.IsFinite(Imaginary);

    public bool IsNaN => double.IsNaN(Real) || double.IsNaN(Imaginary);

    public bool IsInfinity =>
        double.IsInfinity(Real) || double.IsInfinity(Imaginary);

    public static Complex FromDouble(double value) {
        return new Complex(value, 0);
    }

    public static Complex FromBclComplex(BclComplex value) {
        return new Complex(value.Real, value.Imaginary);
    }

    public BclComplex ToBclComplex() {
        return new BclComplex(Real, Imaginary);
    }

    public static Complex FromPolarCoordinates(
        double magnitude,
        double phase) {
        return new Complex(
            magnitude * Math.Cos(phase),
            magnitude * Math.Sin(phase));
    }

    public Complex Conjugate() {
        return new Complex(Real, -Imaginary);
    }

    public Complex Reciprocal() {
        return One / this;
    }

    public static Complex Add(Complex left, Complex right) {
        return new Complex(left.Real + right.Real, left.Imaginary + right.Imaginary);
    }

    public static Complex Subtract(Complex left, Complex right) {
        return new Complex(left.Real - right.Real, left.Imaginary - right.Imaginary);
    }

    public static Complex Negate(Complex value) {
        return new Complex(-value.Real, -value.Imaginary);
    }

    public static Complex Plus(Complex value) {
        return value;
    }

    public static Complex Multiply(Complex left, Complex right) {
        return new Complex(
            (left.Real * right.Real) - (left.Imaginary * right.Imaginary),
            (left.Real * right.Imaginary) + (left.Imaginary * right.Real));
    }

    /// <summary>
    /// Divides two complex values using the ratio form, avoiding overflow in
    /// the intermediate sum of squared components.
    /// </summary>
    public static Complex Divide(Complex dividend, Complex divisor) {
        var real = dividend.Real;
        var imaginary = dividend.Imaginary;
        var divisorReal = divisor.Real;
        var divisorImaginary = divisor.Imaginary;
        if (Math.Abs(divisorReal) >= Math.Abs(divisorImaginary)) {
            var ratio = divisorImaginary / divisorReal;
            var denominator = divisorReal + (divisorImaginary * ratio);
            return new Complex(
                (real + (imaginary * ratio)) / denominator,
                (imaginary - (real * ratio)) / denominator);
        } else {
            var ratio = divisorReal / divisorImaginary;
            var denominator = (divisorReal * ratio) + divisorImaginary;
            return new Complex(
                ((real * ratio) + imaginary) / denominator,
                ((imaginary * ratio) - real) / denominator);
        }
    }

    public static Complex operator +(Complex left, Complex right) {
        return Add(left, right);
    }

    public static Complex operator -(Complex left, Complex right) {
        return Subtract(left, right);
    }

    public static Complex operator -(Complex value) {
        return Negate(value);
    }

    public static Complex operator +(Complex value) {
        return Plus(value);
    }

    public static Complex operator *(Complex left, Complex right) {
        return Multiply(left, right);
    }

    public static Complex operator /(Complex dividend, Complex divisor) {
        return Divide(dividend, divisor);
    }

    public static implicit operator Complex(double value) {
        return FromDouble(value);
    }

    private static double Hypot(double real, double imaginary) {
        return double.Hypot(real, imaginary);
    }
}
