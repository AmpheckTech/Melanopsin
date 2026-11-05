namespace Melanopsin.Mathematics;

/// <summary>
/// A principal-component analysis of centered row-major observations.
/// </summary>
/// <remarks>
/// Components are stored row by row in descending variance order. Each row is
/// a unit-length direction in the original variable space. The analysis uses
/// the population covariance by default; pass a sample covariance request when
/// the observations are a sample. No near-zero eigenvalue is truncated, so
/// callers can apply an explicit relative tolerance when dimensionality
/// reduction requires one.
/// </remarks>
public sealed class PrincipalComponentAnalysisResult {
    private readonly double[] _means;
    private readonly double[] _components;
    private readonly double[] _variances;
    private readonly double[] _explainedVariance;
    private readonly double[] _cumulativeExplainedVariance;
    private readonly double[] _covariance;

    internal PrincipalComponentAnalysisResult(
        double[] means,
        double[] covariance,
        double[] components,
        double[] variances,
        double totalVariance) {
        _means = means;
        _covariance = covariance;
        _components = components;
        _variances = variances;
        _explainedVariance = new double[variances.Length];
        _cumulativeExplainedVariance = new double[variances.Length];
        var cumulative = 0.0;
        for (var index = 0; index < variances.Length; index++) {
            var ratio = variances[index] / totalVariance;
            _explainedVariance[index] = ratio;
            cumulative += ratio;
            _cumulativeExplainedVariance[index] = cumulative;
        }

        VariableCount = means.Length;
        ComponentCount = variances.Length;
    }

    public int VariableCount { get; }

    public int ComponentCount { get; }

    public ReadOnlySpan<double> Means => _means;

    public ReadOnlySpan<double> CovarianceMatrix => _covariance;

    /// <summary>
    /// Gets the principal axes as a component-by-variable row-major matrix.
    /// </summary>
    public ReadOnlySpan<double> Components => _components;

    public ReadOnlySpan<double> Variances => _variances;

    public ReadOnlySpan<double> ExplainedVarianceRatio => _explainedVariance;

    public ReadOnlySpan<double> CumulativeExplainedVarianceRatio =>
        _cumulativeExplainedVariance;

    /// <summary>Projects one observation into principal-component space.</summary>
    public void Transform(ReadOnlySpan<double> observation, Span<double> scores) {
        if (observation.Length < VariableCount) {
            throw new ArgumentException(
                "The observation does not contain enough variables.",
                nameof(observation));
        }

        if (scores.Length < ComponentCount) {
            throw new ArgumentException(
                "The score destination does not contain enough values.",
                nameof(scores));
        }

        if (observation.Overlaps(scores)) {
            throw new ArgumentException(
                "The observation and score destination must not overlap.",
                nameof(scores));
        }

        for (var component = 0; component < ComponentCount; component++) {
            var score = 0.0;
            var offset = component * VariableCount;
            for (var variable = 0; variable < VariableCount; variable++) {
                score += _components[offset + variable] *
                    (observation[variable] - _means[variable]);
            }

            scores[component] = score;
        }
    }
}

public static partial class Statistics {
    /// <summary>
    /// Computes all principal components of row-major observations.
    /// </summary>
    /// <remarks>
    /// Variables are centered by their means. Eigenvalues and components are
    /// sorted in descending variance order. A zero total variance is rejected
    /// because explained-variance ratios are then undefined. The component and
    /// explained-variance conventions follow Jolliffe, <i>Principal Component
    /// Analysis</i>, 2nd ed. (2002),
    /// <see href="https://doi.org/10.1007/b98835">doi:10.1007/b98835</see>.
    /// </remarks>
    public static PrincipalComponentAnalysisResult PrincipalComponentAnalysis(
        ReadOnlySpan<double> observations,
        int observationCount,
        int variableCount,
        bool sampleCovariance = false) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variableCount);
        var covariance = new double[checked(variableCount * variableCount)];
        var means = new double[variableCount];
        if (sampleCovariance) {
            CovarianceMatrixSample(
                observations,
                observationCount,
                variableCount,
                covariance,
                means);
        } else {
            CovarianceMatrixPopulation(
                observations,
                observationCount,
                variableCount,
                covariance,
                means);
        }

        var decomposition = new SymmetricEigenDecomposition(
            covariance,
            variableCount);
        var eigenvalues = decomposition.Eigenvalues;
        var eigenvectors = decomposition.GetEigenvectors();
        var totalVariance = 0.0;
        for (var index = 0; index < eigenvalues.Length; index++) {
            totalVariance += eigenvalues[index];
        }

        if (totalVariance <= 0 || !double.IsFinite(totalVariance)) {
            throw new ArgumentException(
                "Principal components are undefined when total variance is not positive and finite.",
                nameof(observations));
        }

        var components = new double[variableCount * variableCount];
        var variances = new double[variableCount];
        for (var component = 0; component < variableCount; component++) {
            var sourceComponent = variableCount - 1 - component;
            variances[component] = eigenvalues[sourceComponent];
            for (var variable = 0; variable < variableCount; variable++) {
                components[(component * variableCount) + variable] =
                    eigenvectors[(variable * variableCount) + sourceComponent];
            }
        }

        return new PrincipalComponentAnalysisResult(
            means,
            covariance,
            components,
            variances,
            totalVariance);
    }
}
