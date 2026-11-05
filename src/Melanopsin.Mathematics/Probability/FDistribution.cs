// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A central F distribution with two positive degrees-of-freedom parameters.
/// </summary>
public readonly record struct FDistribution : IProbabilityDistribution {
    public FDistribution(double firstDegreesOfFreedom, double secondDegreesOfFreedom) {
        DistributionValidation.Scale(
            firstDegreesOfFreedom,
            nameof(firstDegreesOfFreedom));
        DistributionValidation.Scale(
            secondDegreesOfFreedom,
            nameof(secondDegreesOfFreedom));
        FirstDegreesOfFreedom = firstDegreesOfFreedom;
        SecondDegreesOfFreedom = secondDegreesOfFreedom;
    }

    public double FirstDegreesOfFreedom { get; }

    public double SecondDegreesOfFreedom { get; }

    public double Mean => SecondDegreesOfFreedom > 2
        ? SecondDegreesOfFreedom / (SecondDegreesOfFreedom - 2)
        : double.PositiveInfinity;

    public double Variance {
        get {
            if (SecondDegreesOfFreedom <= 4) {
                return SecondDegreesOfFreedom > 2
                    ? double.PositiveInfinity
                    : double.NaN;
            }

            var denominator = SecondDegreesOfFreedom - 2;
            return 2 * SecondDegreesOfFreedom * SecondDegreesOfFreedom *
                (FirstDegreesOfFreedom + SecondDegreesOfFreedom - 2) /
                (FirstDegreesOfFreedom * denominator * denominator *
                    (SecondDegreesOfFreedom - 4));
        }
    }

    public double Pdf(double value) {
        if (double.IsNaN(value) || value < 0 || double.IsPositiveInfinity(value)) {
            return 0;
        }

        var firstHalf = 0.5 * FirstDegreesOfFreedom;
        var secondHalf = 0.5 * SecondDegreesOfFreedom;
        if (value == 0) {
            return FirstDegreesOfFreedom < 2
                ? double.PositiveInfinity
                : FirstDegreesOfFreedom == 2 ? 1 : 0;
        }

        var logarithm =
            (firstHalf * Math.Log(FirstDegreesOfFreedom)) +
            (secondHalf * Math.Log(SecondDegreesOfFreedom)) +
            ((firstHalf - 1) * Math.Log(value)) -
            ((firstHalf + secondHalf) *
                Math.Log(SecondDegreesOfFreedom +
                    (FirstDegreesOfFreedom * value))) -
            SpecialFunctions.LogBeta(firstHalf, secondHalf);
        return Math.Exp(logarithm);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        return value <= 0
            ? 0
            : double.IsPositiveInfinity(value)
                ? 1
                : SpecialFunctions.IncompleteBetaRegularized(
                    0.5 * FirstDegreesOfFreedom,
                    0.5 * SecondDegreesOfFreedom,
                    FirstDegreesOfFreedom * value /
                        ((FirstDegreesOfFreedom * value) +
                            SecondDegreesOfFreedom));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        var firstShape = 0.5 * FirstDegreesOfFreedom;
        var secondShape = 0.5 * SecondDegreesOfFreedom;
        var betaQuantile = SpecialFunctions.InverseIncompleteBetaRegularized(
            firstShape,
            secondShape,
            probability);
        return SecondDegreesOfFreedom * betaQuantile /
            (FirstDegreesOfFreedom * (1 - betaQuantile));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        var first = DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * FirstDegreesOfFreedom) / FirstDegreesOfFreedom;
        var second = DistributionValidation.SampleStandardGamma(
            ref generator,
            0.5 * SecondDegreesOfFreedom) / SecondDegreesOfFreedom;
        return first / second;
    }
}
