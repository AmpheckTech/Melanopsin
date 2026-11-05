// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A location-scale Student t distribution.
/// </summary>
/// <remarks>
/// The degrees-of-freedom parameter is the only shape parameter. The mean is
/// defined only above one degree of freedom and the variance only above two;
/// otherwise the corresponding property reports NaN or positive infinity to
/// preserve the mathematical domain distinction.
/// </remarks>
public readonly record struct StudentTDistribution : IProbabilityDistribution {
    public StudentTDistribution(double degreesOfFreedom) {
        DistributionValidation.Scale(degreesOfFreedom, nameof(degreesOfFreedom));
        DegreesOfFreedom = degreesOfFreedom;
    }

    public double DegreesOfFreedom { get; }

    public double Mean => DegreesOfFreedom > 1 ? 0 : double.NaN;

    public double Variance => DegreesOfFreedom > 2
        ? DegreesOfFreedom / (DegreesOfFreedom - 2)
        : DegreesOfFreedom > 1
            ? double.PositiveInfinity
            : double.NaN;

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var halfDegrees = 0.5 * DegreesOfFreedom;
        var logarithm =
            SpecialFunctions.LogGamma(halfDegrees + 0.5) -
            SpecialFunctions.LogGamma(halfDegrees) -
            (0.5 * Math.Log(DegreesOfFreedom * Constants.Pi)) -
            ((halfDegrees + 0.5) *
                Math.Log(1 + ((value * value) / DegreesOfFreedom)));
        return Math.Exp(logarithm);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsNegativeInfinity(value)) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        if (value == 0) {
            return 0.5;
        }

        var halfTail = 0.5 * SpecialFunctions.IncompleteBetaRegularized(
            0.5 * DegreesOfFreedom,
            0.5,
            DegreesOfFreedom /
                (DegreesOfFreedom + (value * value)));
        return value > 0 ? 1 - halfTail : halfTail;
    }

    public double Survival(double value) {
        return Cdf(-value);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        if (probability == 0.5) {
            return 0;
        }

        var tail = probability < 0.5 ? probability : 1 - probability;
        var betaQuantile = SpecialFunctions.InverseIncompleteBetaRegularized(
            0.5 * DegreesOfFreedom,
            0.5,
            2 * tail);
        var squared = DegreesOfFreedom *
            FloatingPointMath.Expm1(-Math.Log(betaQuantile));
        return probability < 0.5
            ? -Math.Sqrt(squared)
            : Math.Sqrt(squared);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var normal = DistributionValidation.SampleNormal(ref generator);
        var chiSquare = 2 * DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * DegreesOfFreedom);
        return normal / Math.Sqrt(chiSquare / DegreesOfFreedom);
    }
}
