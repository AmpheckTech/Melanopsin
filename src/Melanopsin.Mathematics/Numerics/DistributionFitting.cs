namespace Melanopsin.Mathematics;

/// <summary>
/// Maximum-likelihood parameter estimates for common continuous
/// distributions.
/// </summary>
/// <remarks>
/// All estimates are for independently and identically distributed
/// observations. Standard errors use the asymptotic Fisher-information
/// approximation and are not confidence intervals. No model-selection or
/// goodness-of-fit decision is implicit.
/// </remarks>
public static class DistributionFitting {
    /// <summary>
    /// Estimates normal-distribution parameters by maximum likelihood.
    /// </summary>
    /// <remarks>
    /// <see cref="NormalFitResult.StandardDeviation"/> is the maximum-likelihood
    /// estimate <c>sqrt(sum((x - mean)^2) / n)</c>, not the unbiased sample
    /// standard deviation. The mean and scale standard errors are
    /// <c>sigma / sqrt(n)</c> and <c>sigma / sqrt(2 n)</c> respectively.
    /// At least two observations are required.
    /// </remarks>
    public static NormalFitResult NormalMle(ReadOnlySpan<double> observations) {
        if (DistributionEvaluation.TryNormalMaximumLikelihood(
                observations,
                out var result,
                out var report)) {
            return result;
        }

        if (report.Status == DistributionStatus.NonFiniteOutput ||
            report.Status == DistributionStatus.NumericalFailure) {
            throw new ArithmeticException(
                "The normal maximum-likelihood estimate is not representable.");
        }

        throw new ArgumentException(
            report.Status == DistributionStatus.DegenerateSample
                ? "The normal maximum-likelihood scale is zero for a degenerate sample."
                : "A finite sample with at least two observations is required.",
            nameof(observations));
    }

    /// <summary>
    /// Estimates an exponential-distribution rate by maximum likelihood.
    /// </summary>
    /// <remarks>
    /// Observations must be non-negative with a positive sample mean. The rate
    /// estimate is the reciprocal sample mean. Its asymptotic standard error is
    /// <c>rate / sqrt(n)</c>; the corresponding mean standard error is
    /// <c>mean / sqrt(n)</c>.
    /// </remarks>
    public static ExponentialFitResult ExponentialMle(
        ReadOnlySpan<double> observations) {
        if (DistributionEvaluation.TryExponentialMaximumLikelihood(
                observations,
                out var result,
                out var report)) {
            return result;
        }

        if (report.Status == DistributionStatus.NonFiniteOutput ||
            report.Status == DistributionStatus.NumericalFailure) {
            throw new ArithmeticException(
                "The exponential maximum-likelihood estimate is not representable.");
        }

        throw new ArgumentException(
            report.Status == DistributionStatus.DegenerateSample
                ? "A positive finite sample mean is required."
                : "Exponential observations must be finite, non-negative, and non-empty.",
            nameof(observations));
    }



}

/// <summary>
/// A maximum-likelihood normal fit and asymptotic parameter standard errors.
/// </summary>
public readonly record struct NormalFitResult(
    double Mean,
    double StandardDeviation,
    double MeanStandardError,
    double StandardDeviationStandardError,
    int Count) {
    /// <summary>Gets the maximum-likelihood variance estimate.</summary>
    public double Variance => StandardDeviation * StandardDeviation;
}

/// <summary>
/// A maximum-likelihood exponential fit and asymptotic parameter standard
/// errors.
/// </summary>
public readonly record struct ExponentialFitResult(
    double Rate,
    double Mean,
    double RateStandardError,
    double MeanStandardError,
    int Count);
