// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Melanopsin.Mathematics;

/// <summary>
/// Fits a probability-distribution model to a sample and reports its
/// log-likelihood.
/// </summary>
/// <param name="observations">Finite observations.</param>
/// <param name="fit">Receives a fitted distribution and its log-likelihood.</param>
/// <returns>
/// <see langword="true"/> when the model is applicable and the fit is finite;
/// otherwise <see langword="false"/>.
/// </returns>
/// <remarks>
/// Implementations must not mutate the observations. A false result means the
/// candidate is inapplicable; thrown exceptions indicate a violated fitter
/// contract and are not converted into model-selection failure.
/// </remarks>
public delegate bool DistributionModelFitter(
    ReadOnlySpan<double> observations,
    out DistributionModelFit fit);

/// <summary>A fitted distribution and the sample log-likelihood at its parameters.</summary>
public readonly record struct DistributionModelFit {
    /// <summary>Initializes a fitted-model result.</summary>
    public DistributionModelFit(
        IProbabilityDistribution distribution,
        double logLikelihood) {
        ArgumentNullException.ThrowIfNull(distribution);
        if (!double.IsFinite(logLikelihood)) {
            throw new ArgumentOutOfRangeException(nameof(logLikelihood));
        }

        Distribution = distribution;
        LogLikelihood = logLikelihood;
    }

    /// <summary>Gets the fitted distribution.</summary>
    public IProbabilityDistribution Distribution { get; }

    /// <summary>Gets the maximized sample log-likelihood.</summary>
    public double LogLikelihood { get; }
}

/// <summary>
/// A named distribution family with an AOT-safe fitting delegate.
/// </summary>
/// <remarks>
/// No reflection, type-name parsing, or runtime code generation is involved.
/// The parameter count is the number of independently estimated model
/// parameters used by the information criteria.
/// </remarks>
public sealed class DistributionModel {
    /// <summary>Initializes a distribution model definition.</summary>
    public DistributionModel(
        string id,
        string displayName,
        int parameterCount,
        DistributionModelFitter fitter) {
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException(
                "A model identifier is required.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException(
                "A display name is required.",
                nameof(displayName));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameterCount);
        ArgumentNullException.ThrowIfNull(fitter);
        Id = id;
        DisplayName = displayName;
        ParameterCount = parameterCount;
        Fitter = fitter;
    }

    /// <summary>Gets the stable machine-readable identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable family name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the number of estimated parameters.</summary>
    public int ParameterCount { get; }

    /// <summary>Gets the fitting delegate.</summary>
    public DistributionModelFitter Fitter { get; }
}

/// <summary>
/// An immutable registry of explicitly constructed probability-distribution
/// models.
/// </summary>
public sealed class DistributionModelCatalog {
    private readonly Dictionary<string, DistributionModel> _byId;
    private readonly ReadOnlyCollection<DistributionModel> _models;

    private DistributionModelCatalog(
        Dictionary<string, DistributionModel> byId,
        DistributionModel[] models) {
        _byId = byId;
        _models = Array.AsReadOnly(models);
    }

    /// <summary>Gets the built-in normal and exponential models.</summary>
    public static DistributionModelCatalog CreateDefault() {
        return CreateBuilder()
            .Add(Normal)
            .Add(Exponential)
            .Build();
    }

    /// <summary>Creates an empty catalog builder.</summary>
    public static Builder CreateBuilder() {
        return new Builder();
    }

    /// <summary>Gets a built-in normal-distribution model.</summary>
    public static DistributionModel Normal { get; } = new(
        "normal",
        "Normal",
        2,
        FitNormal);

    /// <summary>Gets a built-in exponential-distribution model.</summary>
    public static DistributionModel Exponential { get; } = new(
        "exponential",
        "Exponential",
        1,
        FitExponential);

    /// <summary>Gets the registered models in registration order.</summary>
    public IReadOnlyList<DistributionModel> Models => _models;

    /// <summary>Attempts to resolve a model by its case-sensitive identifier.</summary>
    public bool TryGet(
        string id,
        [NotNullWhen(true)] out DistributionModel? model) {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out model);
    }

    private static bool FitNormal(
        ReadOnlySpan<double> observations,
        out DistributionModelFit fit) {
        fit = default;
        if (observations.Length < 2) {
            return false;
        }

        var summary = SampleSummary.Compute(observations);
        if (!summary.IsFinite) {
            return false;
        }

        var variance = summary.SumSquaredDeviations / observations.Length;
        var standardDeviation = Math.Sqrt(variance);
        if (!(standardDeviation > 0) || !double.IsFinite(standardDeviation)) {
            return false;
        }

        var logLikelihood =
            (-0.5 * observations.Length * Math.Log(2 * Constants.Pi * variance)) -
            (0.5 * summary.SumSquaredDeviations / variance);
        if (!double.IsFinite(logLikelihood)) {
            return false;
        }

        fit = new DistributionModelFit(
            new NormalDistribution(summary.Mean, standardDeviation),
            logLikelihood);
        return true;
    }

    private static bool FitExponential(
        ReadOnlySpan<double> observations,
        out DistributionModelFit fit) {
        fit = default;
        if (observations.IsEmpty) {
            return false;
        }

        var sum = 0.0;
        for (var index = 0; index < observations.Length; index++) {
            var value = observations[index];
            if (!double.IsFinite(value) || value < 0) {
                return false;
            }

            sum += value;
        }

        var mean = sum / observations.Length;
        if (!(mean > 0) || !double.IsFinite(mean)) {
            return false;
        }

        var rate = 1 / mean;
        var logLikelihood =
            (observations.Length * Math.Log(rate)) - (rate * sum);
        if (!double.IsFinite(logLikelihood)) {
            return false;
        }

        fit = new DistributionModelFit(
            new ExponentialDistribution(rate),
            logLikelihood);
        return true;
    }

    /// <summary>Builds an immutable distribution-model catalog.</summary>
    public sealed class Builder {
        private readonly Dictionary<string, DistributionModel> _models =
            new(StringComparer.Ordinal);
        private readonly List<DistributionModel> _order = [];

        /// <summary>Adds a model with a unique identifier.</summary>
        public Builder Add(DistributionModel model) {
            ArgumentNullException.ThrowIfNull(model);
            if (!_models.TryAdd(model.Id, model)) {
                throw new ArgumentException(
                    $"Distribution model '{model.Id}' is already registered.",
                    nameof(model));
            }

            _order.Add(model);
            return this;
        }

        /// <summary>Builds an immutable catalog.</summary>
        public DistributionModelCatalog Build() {
            return new DistributionModelCatalog(
                new Dictionary<string, DistributionModel>(
                    _models,
                    StringComparer.Ordinal),
                [.. _order]);
        }
    }
}

/// <summary>Selects the information criterion used for model comparison.</summary>
public enum DistributionModelCriterion {
    /// <summary>Akaike information criterion, <c>2k - 2 ln L</c>.</summary>
    Akaike,

    /// <summary>Small-sample corrected Akaike information criterion.</summary>
    AkaikeCorrected,

    /// <summary>Bayesian information criterion, <c>k ln n - 2 ln L</c>.</summary>
    Bayesian,
}

/// <summary>One fitted model and its information criteria.</summary>
public readonly record struct DistributionModelComparison {
    internal DistributionModelComparison(
        DistributionModel model,
        DistributionModelFit fit,
        int observationCount) {
        Model = model;
        Fit = fit;
        ObservationCount = observationCount;
        var penalty = 2 * model.ParameterCount;
        AkaikeInformationCriterion = penalty - (2 * fit.LogLikelihood);
        BayesianInformationCriterion =
            (model.ParameterCount * Math.Log(observationCount)) -
            (2 * fit.LogLikelihood);
        var correctionDenominator = observationCount - model.ParameterCount - 1;
        AkaikeCorrectedInformationCriterion = correctionDenominator > 0
            ? AkaikeInformationCriterion +
                ((2.0 * model.ParameterCount * (model.ParameterCount + 1)) /
                    correctionDenominator)
            : double.PositiveInfinity;
    }

    /// <summary>Gets the model definition.</summary>
    public DistributionModel Model { get; }

    /// <summary>Gets the fitted distribution and log-likelihood.</summary>
    public DistributionModelFit Fit { get; }

    /// <summary>Gets the number of observations used by the fit.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets the Akaike information criterion.</summary>
    public double AkaikeInformationCriterion { get; }

    /// <summary>Gets the small-sample corrected Akaike information criterion.</summary>
    public double AkaikeCorrectedInformationCriterion { get; }

    /// <summary>Gets the Bayesian information criterion.</summary>
    public double BayesianInformationCriterion { get; }

    /// <summary>Gets the criterion value used for ranking.</summary>
    public double GetCriterion(DistributionModelCriterion criterion) {
        return criterion switch {
            DistributionModelCriterion.Akaike => AkaikeInformationCriterion,
            DistributionModelCriterion.AkaikeCorrected =>
                AkaikeCorrectedInformationCriterion,
            DistributionModelCriterion.Bayesian => BayesianInformationCriterion,
            _ => throw new ArgumentOutOfRangeException(nameof(criterion)),
        };
    }
}

/// <summary>The deterministic result of comparing fitted distribution models.</summary>
public sealed class DistributionModelSelectionResult {
    internal DistributionModelSelectionResult(
        DistributionModelCriterion criterion,
        int observationCount,
        DistributionModelComparison[] comparisons,
        int rejectedModelCount) {
        Criterion = criterion;
        ObservationCount = observationCount;
        Comparisons = Array.AsReadOnly(comparisons);
        RejectedModelCount = rejectedModelCount;
    }

    /// <summary>Gets the ranking criterion.</summary>
    public DistributionModelCriterion Criterion { get; }

    /// <summary>Gets the number of finite observations.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets successful fits ordered from best to worst criterion value.</summary>
    public IReadOnlyList<DistributionModelComparison> Comparisons { get; }

    /// <summary>Gets the best successful fit.</summary>
    public DistributionModelComparison Selected => Comparisons[0];

    /// <summary>Gets the number of candidate models that rejected the sample.</summary>
    public int RejectedModelCount { get; }
}

/// <summary>Information-criterion comparison of fitted distribution models.</summary>
/// <remarks>
/// Lower AIC, AICc, or BIC is preferred. These criteria compare candidates on
/// the same observations; they do not establish that any candidate is
/// scientifically correct, nor do they replace residual or goodness-of-fit
/// analysis. Ties are resolved by ordinal model identifier for determinism.
/// </remarks>
public static class DistributionModelSelector {
    /// <summary>
    /// Fits every registered candidate and ranks successful fits.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The sample is empty or contains a non-finite value.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// No registered model can fit the sample.
    /// </exception>
    public static DistributionModelSelectionResult Select(
        DistributionModelCatalog catalog,
        ReadOnlySpan<double> observations,
        DistributionModelCriterion criterion =
            DistributionModelCriterion.AkaikeCorrected) {
        ArgumentNullException.ThrowIfNull(catalog);
        if (TrySelect(catalog, observations, out var result, out var report, criterion)) {
            return result;
        }

        if (report.Status == DistributionStatus.NoCandidateModel) {
            throw new ArithmeticException(
                "No distribution model could fit the sample.");
        }

        throw new ArgumentException(
            report.Status == DistributionStatus.InsufficientObservations
                ? "At least one observation is required."
                : "The observations and selection criterion must be valid and finite.",
            nameof(observations));
    }

    /// <summary>
    /// Attempts to fit every registered candidate and rank successful models,
    /// reporting invalid samples or the absence of any successful fit.
    /// </summary>
    public static bool TrySelect(
        DistributionModelCatalog catalog,
        ReadOnlySpan<double> observations,
        out DistributionModelSelectionResult result,
        out DistributionOperationReport report,
        DistributionModelCriterion criterion =
            DistributionModelCriterion.AkaikeCorrected) {
        result = null!;
        if (catalog is null ||
            !IsValidCriterion(criterion)) {
            report = new DistributionOperationReport(
                DistributionOperation.ModelSelection,
                DistributionStatus.InvalidInput,
                observations.Length);
            return false;
        }

        if (observations.IsEmpty) {
            report = new DistributionOperationReport(
                DistributionOperation.ModelSelection,
                DistributionStatus.InsufficientObservations);
            return false;
        }

        for (var index = 0; index < observations.Length; index++) {
            if (!double.IsFinite(observations[index])) {
                report = new DistributionOperationReport(
                    DistributionOperation.ModelSelection,
                    DistributionStatus.NonFiniteInput,
                    observations.Length);
                return false;
            }
        }

        var comparisons = new DistributionModelComparison[catalog.Models.Count];
        var successfulCount = 0;
        for (var index = 0; index < catalog.Models.Count; index++) {
            var model = catalog.Models[index];
            if (!model.Fitter(observations, out var fit)) {
                continue;
            }

            comparisons[successfulCount] = new DistributionModelComparison(
                model,
                fit,
                observations.Length);
            successfulCount++;
        }

        if (successfulCount == 0) {
            report = new DistributionOperationReport(
                DistributionOperation.ModelSelection,
                DistributionStatus.NoCandidateModel,
                observations.Length,
                catalog.Models.Count);
            return false;
        }

        if (successfulCount < comparisons.Length) {
            Array.Resize(ref comparisons, successfulCount);
        }

        Sort(comparisons, criterion);
        result = new DistributionModelSelectionResult(
            criterion,
            observations.Length,
            comparisons,
            catalog.Models.Count - successfulCount);
        report = new DistributionOperationReport(
            DistributionOperation.ModelSelection,
            DistributionStatus.Succeeded,
            observations.Length,
            result.RejectedModelCount);
        return true;
    }

    private static void Sort(
        DistributionModelComparison[] comparisons,
        DistributionModelCriterion criterion) {
        for (var index = 1; index < comparisons.Length; index++) {
            var candidate = comparisons[index];
            var cursor = index - 1;
            while (cursor >= 0 && Compare(candidate, comparisons[cursor], criterion) < 0) {
                comparisons[cursor + 1] = comparisons[cursor];
                cursor--;
            }

            comparisons[cursor + 1] = candidate;
        }
    }

    private static int Compare(
        DistributionModelComparison left,
        DistributionModelComparison right,
        DistributionModelCriterion criterion) {
        var leftValue = left.GetCriterion(criterion);
        var rightValue = right.GetCriterion(criterion);
        if (leftValue < rightValue) {
            return -1;
        }

        if (leftValue > rightValue) {
            return 1;
        }

        return string.CompareOrdinal(left.Model.Id, right.Model.Id);
    }

    private static bool IsValidCriterion(
        DistributionModelCriterion criterion) {
        return criterion is DistributionModelCriterion.Akaike or
            DistributionModelCriterion.AkaikeCorrected or
            DistributionModelCriterion.Bayesian;
    }
}

internal readonly record struct SampleSummary(
    double Mean,
    double SumSquaredDeviations) {
    public bool IsFinite =>
        double.IsFinite(Mean) && double.IsFinite(SumSquaredDeviations);

    public static SampleSummary Compute(ReadOnlySpan<double> observations) {
        var mean = 0.0;
        var sumSquaredDeviations = 0.0;
        for (var index = 0; index < observations.Length; index++) {
            var value = observations[index];
            var count = index + 1.0;
            var difference = value - mean;
            mean += difference / count;
            sumSquaredDeviations += difference * (value - mean);
        }

        return new SampleSummary(mean, Math.Max(0, sumSquaredDeviations));
    }
}
