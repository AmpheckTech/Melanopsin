// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A geometric distribution for the number of trials until the first success.
/// </summary>
public readonly record struct GeometricDistribution : IProbabilityDistribution {
    public GeometricDistribution(double probabilityOfSuccess) {
        if (!double.IsFinite(probabilityOfSuccess) ||
            probabilityOfSuccess <= 0 ||
            probabilityOfSuccess > 1) {
            throw new ArgumentOutOfRangeException(nameof(probabilityOfSuccess));
        }

        ProbabilityOfSuccess = probabilityOfSuccess;
    }

    public double ProbabilityOfSuccess { get; }

    public double ProbabilityOfFailure => 1 - ProbabilityOfSuccess;

    public double Mean => 1 / ProbabilityOfSuccess;

    public double Variance => ProbabilityOfFailure /
        (ProbabilityOfSuccess * ProbabilityOfSuccess);

    public double Pdf(double value) {
        if (value < 1 || value != Math.Truncate(value)) {
            return 0;
        }

        return ProbabilityOfSuccess *
            Math.Pow(ProbabilityOfFailure, value - 1);
    }

    /// <summary>
    /// Computes the natural logarithm of the probability mass at a trial
    /// count.
    /// </summary>
    public double LogPmf(double value) {
        if (!double.IsFinite(value) ||
            value < 1 ||
            value != Math.Truncate(value)) {
            return double.NegativeInfinity;
        }

        if (ProbabilityOfSuccess == 1) {
            return value == 1 ? 0 : double.NegativeInfinity;
        }

        return Math.Log(ProbabilityOfSuccess) +
            ((value - 1) * FloatingPointMath.Log1p(-ProbabilityOfSuccess));
    }

    public double Cdf(double value) {
        if (value < 1) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        return -FloatingPointMath.Expm1(
            Math.Floor(value) * FloatingPointMath.Log1p(-ProbabilityOfSuccess));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return 1;
        }

        if (probability == 1 || ProbabilityOfSuccess == 1) {
            return ProbabilityOfSuccess == 1
                ? 1
                : double.PositiveInfinity;
        }

        return Math.Max(
            1,
            Math.Ceiling(
                FloatingPointMath.Log1p(-probability) /
                FloatingPointMath.Log1p(-ProbabilityOfSuccess)));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
