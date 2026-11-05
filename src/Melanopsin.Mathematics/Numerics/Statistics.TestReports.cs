// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented statistical inference procedure.</summary>
public enum StatisticalTestKind {
    /// <summary>Two-sided one-sample Student-t test.</summary>
    OneSampleT,

    /// <summary>Two-sided Welch unequal-variance t test.</summary>
    WelchT,

    /// <summary>Pearson chi-square goodness-of-fit test.</summary>
    ChiSquareGoodnessOfFit,

    /// <summary>Pearson chi-square independence test.</summary>
    ChiSquareIndependence,

    /// <summary>Two-sided variance-ratio F test.</summary>
    VarianceRatioF,

    /// <summary>Two-sided paired Student-t test.</summary>
    PairedT,

    /// <summary>Two-sided Pearson-correlation t test.</summary>
    CorrelationT,

    /// <summary>One-way analysis of variance.</summary>
    OneWayAnova,

    /// <summary>Asymptotic Mann-Whitney U test.</summary>
    MannWhitneyU,

    /// <summary>Asymptotic Wilcoxon signed-rank test.</summary>
    WilcoxonSignedRank,

    /// <summary>Asymptotic Kruskal-Wallis rank test.</summary>
    KruskalWallis,

    /// <summary>Asymptotic Friedman rank test.</summary>
    Friedman,

    /// <summary>Exact paired sign test.</summary>
    Sign,

    /// <summary>Asymptotic Kendall tau-b test.</summary>
    KendallTauB,
}

/// <summary>The termination status of a statistical inference procedure.</summary>
public enum StatisticalTestStatus {
    /// <summary>A finite inferential result was produced.</summary>
    Succeeded,

    /// <summary>
    /// A documented limiting result such as an infinite statistic with zero
    /// p-value was produced from degenerate but exactly representable data.
    /// </summary>
    SucceededDegenerate,

    /// <summary>An input observation or parameter was not finite.</summary>
    NonFiniteInput,

    /// <summary>The sample size or degrees of freedom are insufficient.</summary>
    InsufficientData,

    /// <summary>The supplied observation structure is invalid.</summary>
    InvalidData,

    /// <summary>The statistic or p-value is undefined for the supplied data.</summary>
    DegenerateData,

    /// <summary>A finite input produced a non-representable intermediate or result.</summary>
    NumericalFailure,
}

/// <summary>Describes one implemented statistical inference procedure.</summary>
public readonly record struct StatisticalTestDescriptor {
    internal StatisticalTestDescriptor(
        StatisticalTestKind test,
        string name,
        string nullHypothesis,
        string assumptions,
        string pValueMethod) {
        Test = test;
        Name = name;
        NullHypothesis = nullHypothesis;
        Assumptions = assumptions;
        PValueMethod = pValueMethod;
    }

    /// <summary>Gets the test identifier.</summary>
    public StatisticalTestKind Test { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the tested null hypothesis.</summary>
    public string NullHypothesis { get; }

    /// <summary>Gets the principal distributional assumptions.</summary>
    public string Assumptions { get; }

    /// <summary>Gets the p-value approximation or exactness statement.</summary>
    public string PValueMethod { get; }
}

/// <summary>Diagnostic report for one statistical inference procedure.</summary>
public readonly record struct StatisticalTestReport {
    internal StatisticalTestReport(
        StatisticalTestKind test,
        StatisticalTestStatus status,
        int primarySampleSize,
        int secondarySampleSize,
        double degreesOfFreedom) {
        Test = test;
        Status = status;
        PrimarySampleSize = primarySampleSize;
        SecondarySampleSize = secondarySampleSize;
        DegreesOfFreedom = degreesOfFreedom;
    }

    /// <summary>Gets the executed test.</summary>
    public StatisticalTestKind Test { get; }

    /// <summary>Gets the termination status.</summary>
    public StatisticalTestStatus Status { get; }

    /// <summary>
    /// Gets whether a usable result was produced, including documented
    /// degenerate limiting results.
    /// </summary>
    public bool Succeeded => Status is
        StatisticalTestStatus.Succeeded or
        StatisticalTestStatus.SucceededDegenerate;

    /// <summary>Gets the primary sample, block, or category count.</summary>
    public int PrimarySampleSize { get; }

    /// <summary>Gets the secondary sample count, or zero when not applicable.</summary>
    public int SecondarySampleSize { get; }

    /// <summary>Gets the reported degrees of freedom, or zero when not applicable.</summary>
    public double DegreesOfFreedom { get; }
}

/// <summary>An immutable catalog of implemented statistical inference procedures.</summary>
public static class StatisticalTestCatalog {
    private static readonly StatisticalTestDescriptor[] Entries = [
        new(
            StatisticalTestKind.OneSampleT,
            "One-sample Student t",
            "The population mean equals the specified hypothesized mean.",
            "Independent observations from an approximately normal population with unknown variance.",
            "Exact Student-t reference distribution."),
        new(
            StatisticalTestKind.WelchT,
            "Welch t",
            "Two population means are equal.",
            "Independent samples from approximately normal populations; equal variances are not assumed.",
            "Student-t reference with Welch-Satterthwaite degrees of freedom."),
        new(
            StatisticalTestKind.ChiSquareGoodnessOfFit,
            "Chi-square goodness of fit",
            "Observed category counts follow the specified expected counts.",
            "Independent observations and adequately large expected counts.",
            "Asymptotic chi-square reference distribution."),
        new(
            StatisticalTestKind.ChiSquareIndependence,
            "Chi-square independence",
            "The two categorical variables are independent.",
            "Independent observations and positive row and column margins.",
            "Asymptotic chi-square reference distribution."),
        new(
            StatisticalTestKind.VarianceRatioF,
            "Variance-ratio F",
            "Two population variances are equal.",
            "Independent samples from normal populations.",
            "Two-sided F reference distribution formed from both tails."),
        new(
            StatisticalTestKind.PairedT,
            "Paired Student t",
            "The mean paired difference is zero.",
            "Independent pairs with approximately normal differences and unknown variance.",
            "Exact Student-t reference distribution."),
        new(
            StatisticalTestKind.CorrelationT,
            "Correlation t",
            "The Pearson population correlation is zero.",
            "Bivariate observations with approximately normal marginal behavior.",
            "Student-t transform of the sample correlation."),
        new(
            StatisticalTestKind.OneWayAnova,
            "One-way ANOVA",
            "All group population means are equal.",
            "Independent approximately normal groups with equal population variances.",
            "F reference distribution."),
        new(
            StatisticalTestKind.MannWhitneyU,
            "Mann-Whitney U",
            "The two distributions are stochastically equal.",
            "Independent samples; observations are at least ordinal.",
            "Normal approximation with tie and continuity corrections."),
        new(
            StatisticalTestKind.WilcoxonSignedRank,
            "Wilcoxon signed rank",
            "The paired-difference distribution is symmetric about zero.",
            "Independent pairs; non-zero differences are at least ordinal in magnitude.",
            "Normal approximation with tie and continuity corrections."),
        new(
            StatisticalTestKind.KruskalWallis,
            "Kruskal-Wallis",
            "All group distributions have the same location.",
            "Independent groups; observations are at least ordinal.",
            "Asymptotic chi-square reference with tie correction."),
        new(
            StatisticalTestKind.Friedman,
            "Friedman",
            "Treatments have the same effect within each block.",
            "Complete block designs; observations within a block are at least ordinal.",
            "Asymptotic chi-square reference with tie correction."),
        new(
            StatisticalTestKind.Sign,
            "Paired sign",
            "Positive and negative paired differences are equally likely.",
            "Independent pairs; zero differences are ignored.",
            "Exact central binomial probability."),
        new(
            StatisticalTestKind.KendallTauB,
            "Kendall tau-b",
            "The two variables are independent.",
            "Paired at least-ordinal observations; ties are allowed.",
            "Normal approximation."),
    ];

    /// <summary>Gets the built-in statistical-test catalog.</summary>
    public static ReadOnlySpan<StatisticalTestDescriptor> Tests => Entries;

    /// <summary>Gets the descriptor for an implemented test.</summary>
    public static StatisticalTestDescriptor Get(StatisticalTestKind test) {
        foreach (var entry in Entries) {
            if (entry.Test == test) {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(test));
    }
}
