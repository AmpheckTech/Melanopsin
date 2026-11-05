namespace Melanopsin.Mathematics;

/// <summary>Identifies a regression model form.</summary>
public enum RegressionModelKind {
    /// <summary>Ordinary least squares with an optional intercept.</summary>
    OrdinaryLeastSquares,

    /// <summary>Least squares constrained to have no intercept.</summary>
    ThroughOrigin,

    /// <summary>A polynomial basis in one predictor, with optional intercept.</summary>
    Polynomial,

    /// <summary>Least squares with an L2 penalty on non-intercept coefficients.</summary>
    Ridge,
}

/// <summary>
/// Explicit mathematical requirements used to select a regression method.
/// </summary>
[Flags]
public enum RegressionRequirements {
    /// <summary>No special requirement is asserted.</summary>
    None = 0,

    /// <summary>The model must include an intercept.</summary>
    InterceptRequired = 1,

    /// <summary>The model must not include an intercept.</summary>
    InterceptForbidden = 2,

    /// <summary>The caller has independently established full column rank.</summary>
    FullColumnRankAssumed = 4,

    /// <summary>A rank-deficient design should produce a minimum-norm fit.</summary>
    AllowRankDeficiency = 8,

    /// <summary>An L2 penalty is required for non-intercept coefficients.</summary>
    RegularizationRequired = 16,

    /// <summary>The predictors are a one-dimensional polynomial basis.</summary>
    PolynomialBasis = 32,
}

/// <summary>Identifies the numerical factorization used for regression.</summary>
public enum RegressionSolverMethod {
    /// <summary>Selects a factorization from the explicit model requirements.</summary>
    Auto,

    /// <summary>Householder QR for a full-column-rank design.</summary>
    Qr,

    /// <summary>Singular value decomposition with a relative rank tolerance.</summary>
    Svd,
}

/// <summary>Explains why a regression solver was selected.</summary>
public enum RegressionSelectionReason {
    /// <summary>No selection was required or possible.</summary>
    None,

    /// <summary>Full-column-rank least squares selected Householder QR.</summary>
    FullColumnRank,

    /// <summary>Explicitly allowed rank deficiency selected SVD.</summary>
    RankDeficientMinimumNorm,

    /// <summary>An L2 penalty selected SVD on the augmented least-squares system.</summary>
    RegularizedLeastSquares,
}

/// <summary>Describes one regression model or solver policy in the catalog.</summary>
public readonly record struct RegressionModelDescriptor {
    internal RegressionModelDescriptor(
        RegressionModelKind kind,
        RegressionSolverMethod solver,
        string name,
        bool usesIntercept,
        bool penalizesCoefficients,
        string numericalCharacteristic) {
        Kind = kind;
        Solver = solver;
        Name = name;
        UsesIntercept = usesIntercept;
        PenalizesCoefficients = penalizesCoefficients;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the model identifier.</summary>
    public RegressionModelKind Kind { get; }

    /// <summary>Gets the recommended numerical solver.</summary>
    public RegressionSolverMethod Solver { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the model includes an intercept by default.</summary>
    public bool UsesIntercept { get; }

    /// <summary>Gets whether non-intercept coefficients receive an L2 penalty.</summary>
    public bool PenalizesCoefficients { get; }

    /// <summary>Gets the principal conditioning and rank trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>
/// An immutable, allocation-free catalog of regression policies.
/// </summary>
/// <remarks>
/// Selection uses only requirements declared by the caller. It does not scan
/// predictor values, infer rank from an absolute coefficient threshold, or
/// silently convert a rank-deficient ordinary fit into a regularized fit.
/// </remarks>
public static class RegressionCatalog {
    private static readonly RegressionModelDescriptor[] Entries = [
        new(
            RegressionModelKind.OrdinaryLeastSquares,
            RegressionSolverMethod.Qr,
            "Ordinary least squares",
            usesIntercept: true,
            penalizesCoefficients: false,
            "Householder QR is stable for full-column-rank designs without forming normal equations."),
        new(
            RegressionModelKind.ThroughOrigin,
            RegressionSolverMethod.Qr,
            "Least squares through the origin",
            usesIntercept: false,
            penalizesCoefficients: false,
            "Householder QR with the intercept column omitted."),
        new(
            RegressionModelKind.Polynomial,
            RegressionSolverMethod.Qr,
            "Polynomial least squares",
            usesIntercept: true,
            penalizesCoefficients: false,
            "QR on an explicit ascending-power basis; high degrees become ill-conditioned."),
        new(
            RegressionModelKind.Ridge,
            RegressionSolverMethod.Svd,
            "Ridge regression",
            usesIntercept: true,
            penalizesCoefficients: true,
            "SVD on a row-augmented least-squares system; the penalty never applies to the intercept."),
    ];

    /// <summary>Gets the built-in regression catalog.</summary>
    public static ReadOnlySpan<RegressionModelDescriptor> Models => Entries;

    /// <summary>
    /// Selects a model and solver from explicit requirements.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Intercept requirements conflict.
    /// </exception>
    public static RegressionModelKind Recommend(
        RegressionRequirements requirements,
        out RegressionSolverMethod solver,
        out RegressionSelectionReason reason) {
        if ((requirements & RegressionRequirements.InterceptRequired) != 0 &&
            (requirements & RegressionRequirements.InterceptForbidden) != 0) {
            throw new ArgumentException(
                "Intercept requirements cannot be both required and forbidden.",
                nameof(requirements));
        }

        if ((requirements & RegressionRequirements.RegularizationRequired) != 0) {
            solver = RegressionSolverMethod.Svd;
            reason = RegressionSelectionReason.RegularizedLeastSquares;
            return RegressionModelKind.Ridge;
        }

        if ((requirements & RegressionRequirements.PolynomialBasis) != 0) {
            solver = (requirements & RegressionRequirements.AllowRankDeficiency) != 0
                ? RegressionSolverMethod.Svd
                : RegressionSolverMethod.Qr;
            reason = solver == RegressionSolverMethod.Svd
                ? RegressionSelectionReason.RankDeficientMinimumNorm
                : RegressionSelectionReason.FullColumnRank;
            return RegressionModelKind.Polynomial;
        }

        if ((requirements & RegressionRequirements.InterceptForbidden) != 0) {
            solver = (requirements & RegressionRequirements.AllowRankDeficiency) != 0
                ? RegressionSolverMethod.Svd
                : RegressionSolverMethod.Qr;
            reason = solver == RegressionSolverMethod.Svd
                ? RegressionSelectionReason.RankDeficientMinimumNorm
                : RegressionSelectionReason.FullColumnRank;
            return RegressionModelKind.ThroughOrigin;
        }

        solver = (requirements & RegressionRequirements.AllowRankDeficiency) != 0
            ? RegressionSolverMethod.Svd
            : RegressionSolverMethod.Qr;
        reason = solver == RegressionSolverMethod.Svd
            ? RegressionSelectionReason.RankDeficientMinimumNorm
            : RegressionSelectionReason.FullColumnRank;
        return RegressionModelKind.OrdinaryLeastSquares;
    }
}

/// <summary>The termination status of a regression fit.</summary>
public enum RegressionFitStatus {
    /// <summary>A finite coefficient vector and finite diagnostics were produced.</summary>
    Succeeded,

    /// <summary>The design is rank deficient under the selected method or tolerance.</summary>
    RankDeficient,

    /// <summary>At least one supplied value was not finite.</summary>
    NonFiniteInput,

    /// <summary>The selected solver is incompatible with the model or design shape.</summary>
    Unsupported,

    /// <summary>Finite coefficients and diagnostics could not be produced.</summary>
    NumericalFailure,
}

/// <summary>Diagnostic summary of one regression fit.</summary>
public readonly record struct RegressionFitReport {
    internal RegressionFitReport(
        RegressionModelKind modelKind,
        RegressionSolverMethod solverMethod,
        RegressionSelectionReason selectionReason,
        RegressionFitStatus status,
        int numericalRank,
        int observationCount,
        int designColumnCount,
        int totalDegreesOfFreedom,
        int residualDegreesOfFreedom,
        double residualSumOfSquares,
        double totalSumOfSquares,
        double rSquared,
        double adjustedRSquared) {
        ModelKind = modelKind;
        SolverMethod = solverMethod;
        SelectionReason = selectionReason;
        Status = status;
        NumericalRank = numericalRank;
        ObservationCount = observationCount;
        DesignColumnCount = designColumnCount;
        TotalDegreesOfFreedom = totalDegreesOfFreedom;
        ResidualDegreesOfFreedom = residualDegreesOfFreedom;
        ResidualSumOfSquares = residualSumOfSquares;
        TotalSumOfSquares = totalSumOfSquares;
        RSquared = rSquared;
        AdjustedRSquared = adjustedRSquared;
    }

    /// <summary>Gets the fitted model form.</summary>
    public RegressionModelKind ModelKind { get; }

    /// <summary>Gets the solver that was actually executed.</summary>
    public RegressionSolverMethod SolverMethod { get; }

    /// <summary>Gets the automatic-selection rationale, if applicable.</summary>
    public RegressionSelectionReason SelectionReason { get; }

    /// <summary>Gets the termination status.</summary>
    public RegressionFitStatus Status { get; }

    /// <summary>Gets whether the fit succeeded.</summary>
    public bool Succeeded => Status == RegressionFitStatus.Succeeded;

    /// <summary>
    /// Gets the numerical rank of the original design under the selected rank
    /// policy. Ridge augmentation is diagnostic implementation detail and is
    /// not counted here.
    /// </summary>
    public int NumericalRank { get; }

    /// <summary>Gets the number of original observations.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets the number of coefficients in the fitted model.</summary>
    public int DesignColumnCount { get; }

    /// <summary>
    /// Gets the total degrees of freedom used by adjusted R-squared:
    /// <c>n - 1</c> for intercept models and <c>n</c> for intercept-free
    /// models. Zero means the diagnostic is undefined.
    /// </summary>
    public int TotalDegreesOfFreedom { get; }

    /// <summary>
    /// Gets the residual degrees of freedom <c>n - rank</c>, or zero when the
    /// fit failed or the residual diagnostic is undefined.
    /// </summary>
    public int ResidualDegreesOfFreedom { get; }

    /// <summary>Gets the original-scale residual sum of squares.</summary>
    public double ResidualSumOfSquares { get; }

    /// <summary>
    /// Gets the total response sum of squares. It is centered for intercept
    /// models and uncentered, <c>sum(y^2)</c>, for intercept-free models.
    /// </summary>
    public double TotalSumOfSquares { get; }

    /// <summary>Gets the coefficient of determination.</summary>
    public double RSquared { get; }

    /// <summary>
    /// Gets the degrees-of-freedom-adjusted coefficient of determination, or
    /// NaN when R-squared or the required degrees of freedom are undefined.
    /// </summary>
    public double AdjustedRSquared { get; }
}

/// <summary>An immutable finite regression fit.</summary>
public sealed class RegressionFitResult {
    private readonly double[] _coefficients;

    internal RegressionFitResult(
        double[] coefficients,
        bool hasIntercept,
        int predictorCount,
        RegressionFitReport report) {
        _coefficients = coefficients;
        HasIntercept = hasIntercept;
        PredictorCount = predictorCount;
        Report = report;
    }

    /// <summary>Gets the coefficients in model order.</summary>
    public ReadOnlySpan<double> Coefficients => _coefficients;

    /// <summary>
    /// Gets whether the first coefficient is an intercept. This is always true
    /// for a successful degree-zero polynomial fit.
    /// </summary>
    public bool HasIntercept { get; }

    /// <summary>Gets the number of original predictor columns.</summary>
    public int PredictorCount { get; }

    /// <summary>Gets the fit diagnostics.</summary>
    public RegressionFitReport Report { get; }

    /// <summary>Evaluates the fitted model at one predictor vector.</summary>
    public double Predict(ReadOnlySpan<double> predictors) {
        if (predictors.Length != PredictorCount) {
            throw new ArgumentException(
                "The predictor vector length does not match the fitted model.",
                nameof(predictors));
        }

        var coefficientIndex = 0;
        var result = HasIntercept ? _coefficients[coefficientIndex++] : 0;
        for (var index = 0; index < predictors.Length; index++) {
            if (!double.IsFinite(predictors[index])) {
                throw new ArgumentException(
                    "Predictors must contain only finite values.",
                    nameof(predictors));
            }

            result += _coefficients[coefficientIndex++] * predictors[index];
        }

        return result;
    }
}
