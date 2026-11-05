// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Computes a two-sided Student-t confidence interval for a population mean.
    /// </summary>
    /// <remarks>
    /// This is the conventional interval for independent observations from an
    /// approximately normal population with an unknown variance. It is not a
    /// distribution-free interval. The confidence level must be strictly
    /// between zero and one; for example, pass <c>0.95</c> for a 95% interval.
    /// </remarks>
    public static Interval MeanConfidenceInterval(
        ReadOnlySpan<double> observations,
        double confidenceLevel = 0.95) {
        if (observations.Length < 2) {
            throw new ArgumentException(
                "At least two observations are required.",
                nameof(observations));
        }

        ValidateConfidenceLevel(confidenceLevel);
        var mean = Mean(observations);
        var standardError = StandardErrorOfMean(observations);
        var criticalValue = new StudentTDistribution(
            observations.Length - 1).Quantile(
                1 - (0.5 * (1 - confidenceLevel)));
        var margin = criticalValue * standardError;
        return FiniteInterval(mean - margin, mean + margin);
    }

    /// <summary>
    /// Computes a two-sided normal confidence interval for a mean with a known
    /// population variance.
    /// </summary>
    public static Interval MeanConfidenceIntervalKnownVariance(
        ReadOnlySpan<double> observations,
        double populationVariance,
        double confidenceLevel = 0.95) {
        if (observations.IsEmpty) {
            throw new ArgumentException(
                "At least one observation is required.",
                nameof(observations));
        }

        if (!double.IsFinite(populationVariance) || populationVariance < 0) {
            throw new ArgumentOutOfRangeException(nameof(populationVariance));
        }

        ValidateConfidenceLevel(confidenceLevel);
        var mean = Mean(observations);
        var criticalValue = NormalCriticalValue(confidenceLevel);
        var standardError = Math.Sqrt(populationVariance / observations.Length);
        var margin = criticalValue * standardError;
        return FiniteInterval(mean - margin, mean + margin);
    }

    /// <summary>
    /// Computes a two-sided Welch confidence interval for a difference of
    /// population means.
    /// </summary>
    /// <remarks>
    /// The interval does not assume equal population variances. Its degrees of
    /// freedom use the Welch-Satterthwaite approximation.
    /// </remarks>
    public static Interval DifferenceOfMeansConfidenceInterval(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        double confidenceLevel = 0.95) {
        ValidateTwoSamples(first, second);
        ValidateConfidenceLevel(confidenceLevel);
        var difference = Mean(first) - Mean(second);
        var firstComponent = VarianceSample(first) / first.Length;
        var secondComponent = VarianceSample(second) / second.Length;
        var standardError = Math.Sqrt(firstComponent + secondComponent);
        if (standardError == 0) {
            return FiniteInterval(difference, difference);
        }

        var degreesOfFreedom =
            ((firstComponent + secondComponent) * (firstComponent + secondComponent)) /
            (((firstComponent * firstComponent) / (first.Length - 1)) +
                ((secondComponent * secondComponent) / (second.Length - 1)));
        var criticalValue = new StudentTDistribution(degreesOfFreedom).Quantile(
            1 - (0.5 * (1 - confidenceLevel)));
        var margin = criticalValue * standardError;
        return FiniteInterval(difference - margin, difference + margin);
    }

    /// <summary>
    /// Computes the Wilson score interval for a binomial proportion.
    /// </summary>
    /// <remarks>
    /// The Wilson interval is bounded to [0, 1] and generally has better
    /// small-sample coverage than the normal approximation. It is still an
    /// asymptotic interval and does not replace an exact interval when exact
    /// coverage is required.
    /// </remarks>
    public static Interval ProportionWilsonConfidenceInterval(
        int successes,
        int trials,
        double confidenceLevel = 0.95) {
        ValidateBinomialCounts(successes, trials);
        ValidateConfidenceLevel(confidenceLevel);
        var count = (double)trials;
        var proportion = (double)successes / count;
        var criticalValue = NormalCriticalValue(confidenceLevel);
        var squaredCriticalValue = criticalValue * criticalValue;
        var denominator = 1 + (squaredCriticalValue / count);
        var center =
            (proportion + (squaredCriticalValue / (2 * count))) / denominator;
        var halfWidth =
            (criticalValue / denominator) * Math.Sqrt(
                ((proportion * (1 - proportion)) / count) +
                (squaredCriticalValue / (4 * count * count)));
        return FiniteInterval(
            Math.Max(0, center - halfWidth),
            Math.Min(1, center + halfWidth));
    }

    /// <summary>
    /// Computes the exact Clopper-Pearson interval for a binomial proportion.
    /// </summary>
    /// <remarks>
    /// The interval guarantees at least the requested coverage for every true
    /// proportion under independent Bernoulli trials. It is intentionally
    /// conservative and can be wider than the Wilson interval.
    /// </remarks>
    public static Interval ProportionClopperPearsonConfidenceInterval(
        int successes,
        int trials,
        double confidenceLevel = 0.95) {
        ValidateBinomialCounts(successes, trials);
        ValidateConfidenceLevel(confidenceLevel);
        var tailProbability = 0.5 * (1 - confidenceLevel);
        var lower = successes == 0
            ? 0
            : new BetaDistribution(
                successes,
                trials - successes + 1).Quantile(tailProbability);
        var upper = successes == trials
            ? 1
            : new BetaDistribution(
                successes + 1,
                trials - successes).Quantile(1 - tailProbability);
        return FiniteInterval(lower, upper);
    }

    /// <summary>
    /// Computes a two-sided chi-square confidence interval for a normal
    /// population variance.
    /// </summary>
    public static Interval VarianceConfidenceInterval(
        ReadOnlySpan<double> observations,
        double confidenceLevel = 0.95) {
        if (observations.Length < 2) {
            throw new ArgumentException(
                "At least two observations are required.",
                nameof(observations));
        }

        ValidateConfidenceLevel(confidenceLevel);
        var degreesOfFreedom = observations.Length - 1;
        var variance = VarianceSample(observations);
        var tailProbability = 0.5 * (1 - confidenceLevel);
        var lowerCritical = new ChiSquareDistribution(
            degreesOfFreedom).Quantile(1 - tailProbability);
        var upperCritical = new ChiSquareDistribution(
            degreesOfFreedom).Quantile(tailProbability);
        return FiniteInterval(
            (degreesOfFreedom * variance) / lowerCritical,
            (degreesOfFreedom * variance) / upperCritical);
    }

    /// <summary>
    /// Computes a two-sided chi-square confidence interval for a normal
    /// population standard deviation.
    /// </summary>
    public static Interval StandardDeviationConfidenceInterval(
        ReadOnlySpan<double> observations,
        double confidenceLevel = 0.95) {
        var varianceInterval = VarianceConfidenceInterval(
            observations,
            confidenceLevel);
        return FiniteInterval(
            Math.Sqrt(varianceInterval.Lower),
            Math.Sqrt(varianceInterval.Upper));
    }

    /// <summary>
    /// Performs a two-sided one-sample Student-t test of the population mean.
    /// </summary>
    public static OneSampleTTestResult OneSampleTTest(
        ReadOnlySpan<double> observations,
        double hypothesizedMean = 0) {
        if (observations.Length < 2) {
            throw new ArgumentException(
                "At least two observations are required.",
                nameof(observations));
        }

        if (!double.IsFinite(hypothesizedMean)) {
            throw new ArgumentOutOfRangeException(nameof(hypothesizedMean));
        }

        var standardError = StandardErrorOfMean(observations);
        var difference = Mean(observations) - hypothesizedMean;
        var statistic = difference / standardError;
        var degreesOfFreedom = observations.Length - 1;
        var pValue = TwoSidedStudentTPValue(statistic, degreesOfFreedom);
        return new OneSampleTTestResult(
            statistic,
            degreesOfFreedom,
            pValue);
    }

    /// <summary>
    /// Performs a two-sided Welch t test for a difference of population means.
    /// </summary>
    /// <remarks>
    /// Reference: Welch (1947),
    /// <see href="https://doi.org/10.1093/biomet/34.1-2.28">doi:10.1093/biomet/34.1-2.28</see>.
    /// </remarks>
    public static WelchTTestResult WelchTTest(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateTwoSamples(first, second);
        var difference = Mean(first) - Mean(second);
        var firstComponent = VarianceSample(first) / first.Length;
        var secondComponent = VarianceSample(second) / second.Length;
        var standardError = Math.Sqrt(firstComponent + secondComponent);
        if (standardError == 0) {
            return new WelchTTestResult(
                difference,
                0,
                double.PositiveInfinity,
                difference == 0 ? 1 : 0);
        }

        var degreesOfFreedom =
            ((firstComponent + secondComponent) * (firstComponent + secondComponent)) /
            (((firstComponent * firstComponent) / (first.Length - 1)) +
                ((secondComponent * secondComponent) / (second.Length - 1)));
        var statistic = difference / standardError;
        return new WelchTTestResult(
            difference,
            statistic,
            degreesOfFreedom,
            TwoSidedStudentTPValue(statistic, degreesOfFreedom));
    }

    private static double NormalCriticalValue(double confidenceLevel) {
        return SpecialFunctions.InverseNormalCdf(
            1 - (0.5 * (1 - confidenceLevel)));
    }

    private static double TwoSidedStudentTPValue(
        double statistic,
        double degreesOfFreedom) {
        if (double.IsNaN(statistic)) {
            return double.NaN;
        }

        return 2 * new StudentTDistribution(degreesOfFreedom).Cdf(
            -Math.Abs(statistic));
    }

    private static Interval FiniteInterval(double lower, double upper) {
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArithmeticException(
                "The confidence interval is not representable with finite endpoints.");
        }

        return lower <= upper
            ? new Interval(lower, upper)
            : new Interval(upper, lower);
    }

    private static void ValidateConfidenceLevel(double confidenceLevel) {
        if (!double.IsFinite(confidenceLevel) ||
            confidenceLevel <= 0 ||
            confidenceLevel >= 1) {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                "The confidence level must be finite and in (0, 1).");
        }
    }

    private static void ValidateBinomialCounts(int successes, int trials) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trials);
        ArgumentOutOfRangeException.ThrowIfNegative(successes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(successes, trials);
    }

    private static void ValidateTwoSamples(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        if (first.Length < 2) {
            throw new ArgumentException(
                "At least two values are required in the first sample.",
                nameof(first));
        }

        if (second.Length < 2) {
            throw new ArgumentException(
                "At least two values are required in the second sample.",
                nameof(second));
        }
    }
}

/// <summary>
/// The result of a two-sided one-sample Student-t test.
/// </summary>
public readonly record struct OneSampleTTestResult(
    double Statistic,
    double DegreesOfFreedom,
    double TwoSidedPValue);

/// <summary>
/// The result of a two-sided Welch unequal-variance t test.
/// </summary>
public readonly record struct WelchTTestResult(
    double DifferenceOfMeans,
    double Statistic,
    double DegreesOfFreedom,
    double TwoSidedPValue);
