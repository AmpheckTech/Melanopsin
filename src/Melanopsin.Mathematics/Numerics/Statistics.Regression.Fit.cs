namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Fits an explicit regression model using a full-column-rank QR or a
    /// relative-tolerance SVD policy.
    /// </summary>
    /// <param name="predictors">
    /// Row-major predictor storage with at least
    /// <c>rowCount * predictorCount</c> values.
    /// </param>
    /// <param name="rowCount">The number of observations.</param>
    /// <param name="predictorCount">The number of predictor columns.</param>
    /// <param name="response">At least <paramref name="rowCount"/> values.</param>
    /// <param name="modelKind">The requested model form.</param>
    /// <param name="includeIntercept">
    /// Whether to prepend an intercept column. This is forced to false for
    /// <see cref="RegressionModelKind.ThroughOrigin"/>.
    /// </param>
    /// <param name="ridgePenalty">
    /// Non-negative ridge penalty on non-intercept coefficients. It must be
    /// positive for <see cref="RegressionModelKind.Ridge"/> and zero otherwise.
    /// The objective is the unnormalized
    /// <c>||X b - y||^2 + lambda ||b_without_intercept||^2</c>.
    /// </param>
    /// <param name="solverMethod">The factorization policy.</param>
    /// <param name="relativeRankTolerance">
    /// Singular values at or below this fraction of the largest singular value
    /// are treated as zero by SVD. Ridge fitting uses this relative policy for
    /// both the augmented solve and the reported original-design rank. Zero
    /// means exact nonzero.
    /// </param>
    /// <remarks>
    /// <para>
    /// The penalty is never applied to the intercept. A zero penalty on an
    /// otherwise rank-deficient ordinary design is reported as
    /// <see cref="RegressionFitStatus.RankDeficient"/> unless the caller
    /// explicitly selects SVD. No absolute determinant or coefficient
    /// threshold is used.
    /// </para>
    /// <para>
    /// <see cref="RegressionFitReport.TotalSumOfSquares"/> and R-squared are
    /// centered when an intercept is present and uncentered for a
    /// through-origin or otherwise intercept-free model. Adjusted R-squared
    /// uses <c>n - 1</c> total degrees of freedom with an intercept and
    /// <c>n</c> without one, combined with <c>n - rank</c> residual degrees of
    /// freedom. It is NaN when either residual degrees of freedom or the total
    /// degrees of freedom are zero.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Dimensions, the penalty, or the relative rank tolerance are invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A buffer is too short, or the model request is internally inconsistent.
    /// </exception>
    public static RegressionFitResult FitRegression(
        ReadOnlySpan<double> predictors,
        int rowCount,
        int predictorCount,
        ReadOnlySpan<double> response,
        RegressionModelKind modelKind,
        bool includeIntercept = true,
        double ridgePenalty = 0,
        RegressionSolverMethod solverMethod = RegressionSolverMethod.Auto,
        double relativeRankTolerance = 0) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(predictorCount);
        if (!double.IsFinite(ridgePenalty) || ridgePenalty < 0) {
            throw new ArgumentOutOfRangeException(nameof(ridgePenalty));
        }

        if (!double.IsFinite(relativeRankTolerance) ||
            relativeRankTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeRankTolerance));
        }

        if (modelKind == RegressionModelKind.Ridge) {
            if (ridgePenalty <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(ridgePenalty),
                    "Ridge regression requires a positive penalty.");
            }
        } else if (ridgePenalty != 0) {
            throw new ArgumentException(
                "A nonzero ridge penalty requires the ridge model kind.",
                nameof(ridgePenalty));
        }

        var effectiveIntercept =
            modelKind != RegressionModelKind.ThroughOrigin && includeIntercept;
        var designColumnCount = checked(
            predictorCount + (effectiveIntercept ? 1 : 0));
        var predictorElementCount = checked(rowCount * predictorCount);
        if (predictors.Length < predictorElementCount) {
            throw new ArgumentException(
                "The predictor storage is too small.",
                nameof(predictors));
        }

        if (response.Length < rowCount) {
            throw new ArgumentException(
                "The response is too small.",
                nameof(response));
        }

        var design = new double[checked(rowCount * designColumnCount)];
        for (var row = 0; row < rowCount; row++) {
            var designOffset = row * designColumnCount;
            var predictorOffset = row * predictorCount;
            var column = 0;
            if (effectiveIntercept) {
                design[designOffset] = 1;
                column = 1;
            }

            for (var predictor = 0; predictor < predictorCount; predictor++) {
                var value = predictors[predictorOffset + predictor];
                if (!double.IsFinite(value)) {
                    return Failure(
                        modelKind,
                        solverMethod,
                        RegressionSelectionReason.None,
                        RegressionFitStatus.NonFiniteInput,
                        effectiveIntercept,
                        predictorCount,
                        designColumnCount,
                        new double[designColumnCount],
                        rowCount);
                }

                design[designOffset + column + predictor] = value;
            }

            if (!double.IsFinite(response[row])) {
                return Failure(
                    modelKind,
                    solverMethod,
                    RegressionSelectionReason.None,
                    RegressionFitStatus.NonFiniteInput,
                    effectiveIntercept,
                    predictorCount,
                    designColumnCount,
                    new double[designColumnCount],
                    rowCount);
            }
        }

        var originalDesign = design;
        var rightHandSide = response[..rowCount].ToArray();
        if (modelKind == RegressionModelKind.Ridge) {
            var penaltyColumnCount = predictorCount;
            design = new double[
                checked((rowCount + penaltyColumnCount) * designColumnCount)];
            originalDesign.CopyTo(design, 0);
            rightHandSide = new double[checked(rowCount + penaltyColumnCount)];
            response[..rowCount].CopyTo(rightHandSide);
            var penaltyScale = Math.Sqrt(ridgePenalty);
            var penaltyOffset = effectiveIntercept ? 1 : 0;
            for (var column = 0; column < penaltyColumnCount; column++) {
                design[
                    ((rowCount + column) * designColumnCount) +
                    penaltyOffset +
                    column] = penaltyScale;
            }
        }

        var selectionReason = RegressionSelectionReason.None;
        if (solverMethod == RegressionSolverMethod.Auto) {
            solverMethod = RegressionSolverMethod.Qr;
            selectionReason = RegressionSelectionReason.FullColumnRank;
            if (modelKind == RegressionModelKind.Ridge ||
                design.GetLength(0) < designColumnCount) {
                solverMethod = RegressionSolverMethod.Svd;
                selectionReason = modelKind == RegressionModelKind.Ridge
                    ? RegressionSelectionReason.RegularizedLeastSquares
                    : RegressionSelectionReason.RankDeficientMinimumNorm;
            }
        }

        if (solverMethod == RegressionSolverMethod.Qr &&
            design.GetLength(0) < designColumnCount) {
            return Failure(
                modelKind,
                solverMethod,
                selectionReason,
                RegressionFitStatus.Unsupported,
                effectiveIntercept,
                predictorCount,
                designColumnCount,
                new double[designColumnCount],
                rowCount);
        }

        var coefficients = new double[designColumnCount];
        var linearReport = default(LinearSolveReport);
        var succeeded = DenseLinearSystemSolver.TrySolve(
            design,
            design.GetLength(0),
            designColumnCount,
            rightHandSide,
            coefficients,
            MatrixStructure.None,
            out linearReport,
            solverMethod == RegressionSolverMethod.Qr
                ? LinearSolverMethod.Qr
                : LinearSolverMethod.Svd,
            relativeRankTolerance);
        if (!succeeded) {
            var status = LinearSolveStatusToRegressionStatus(linearReport.Status);
            return Failure(
                modelKind,
                solverMethod,
                selectionReason,
                status,
                effectiveIntercept,
                predictorCount,
                designColumnCount,
                new double[designColumnCount],
                rowCount,
                linearReport.NumericalRank);
        }

        var numericalRank = solverMethod == RegressionSolverMethod.Svd
            ? linearReport.NumericalRank
            : designColumnCount;
        if (modelKind == RegressionModelKind.Ridge) {
            // Ridge augments the design with penalty rows. The caller-facing
            // rank always refers to the original observation design, never to
            // that implementation-only augmented matrix.
            numericalRank = new SvdDecomposition(
                originalDesign,
                rowCount,
                designColumnCount).CountRank(relativeRankTolerance);
        }

        if (numericalRank < designColumnCount &&
            modelKind != RegressionModelKind.Ridge &&
            solverMethod != RegressionSolverMethod.Svd) {
            return Failure(
                modelKind,
                solverMethod,
                selectionReason,
                RegressionFitStatus.RankDeficient,
                effectiveIntercept,
                predictorCount,
                designColumnCount,
                new double[designColumnCount],
                rowCount,
                numericalRank);
        }

        var mean = Mean(response[..rowCount]);
        var residualSumOfSquares = 0.0;
        var totalSumOfSquares = 0.0;
        for (var row = 0; row < rowCount; row++) {
            var fitted = 0.0;
            var designOffset = row * designColumnCount;
            for (var column = 0; column < designColumnCount; column++) {
                fitted += originalDesign[designOffset + column] * coefficients[column];
            }

            var residual = response[row] - fitted;
            residualSumOfSquares += residual * residual;
            var totalValue = effectiveIntercept
                ? response[row] - mean
                : response[row];
            totalSumOfSquares += totalValue * totalValue;
        }

        if (!double.IsFinite(residualSumOfSquares) ||
            !double.IsFinite(totalSumOfSquares) ||
            !double.IsFinite(mean)) {
            return Failure(
                modelKind,
                solverMethod,
                selectionReason,
                RegressionFitStatus.NumericalFailure,
                effectiveIntercept,
                predictorCount,
                designColumnCount,
                new double[designColumnCount],
                rowCount,
                numericalRank);
        }

        var totalDegreesOfFreedom = Math.Max(
            0,
            rowCount - (effectiveIntercept ? 1 : 0));
        var residualDegreesOfFreedom = Math.Max(0, rowCount - numericalRank);
        var rSquared = totalSumOfSquares == 0
            ? (residualSumOfSquares == 0 ? 1 : double.NaN)
            : 1 - (residualSumOfSquares / totalSumOfSquares);
        var adjustedRSquared = residualDegreesOfFreedom == 0 ||
            totalDegreesOfFreedom == 0 ||
            double.IsNaN(rSquared)
            ? double.NaN
            : 1 - ((1 - rSquared) *
                ((double)totalDegreesOfFreedom / residualDegreesOfFreedom));
        var report = new RegressionFitReport(
            modelKind,
            solverMethod,
            selectionReason,
            RegressionFitStatus.Succeeded,
            numericalRank,
            rowCount,
            designColumnCount,
            totalDegreesOfFreedom,
            residualDegreesOfFreedom,
            residualSumOfSquares,
            totalSumOfSquares,
            rSquared,
            adjustedRSquared);
        return new RegressionFitResult(
            coefficients,
            effectiveIntercept,
            predictorCount,
            report);
    }

    /// <summary>
    /// Fits an ascending-power polynomial least-squares model.
    /// </summary>
    /// <remarks>
    /// The design columns are <c>1, x, ..., x^degree</c> when an intercept is
    /// included and <c>x, ..., x^degree</c> otherwise. Coefficients are stored
    /// in ascending power order. High degrees can be ill-conditioned; callers
    /// that permit numerical rank loss should explicitly select SVD and a
    /// relative rank tolerance.
    /// </remarks>
    public static RegressionFitResult FitPolynomialRegression(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int degree,
        bool includeIntercept = true,
        RegressionSolverMethod solverMethod = RegressionSolverMethod.Auto,
        double relativeRankTolerance = 0) {
        if (solverMethod is not (RegressionSolverMethod.Auto or
            RegressionSolverMethod.Qr or RegressionSolverMethod.Svd)) {
            throw new ArgumentOutOfRangeException(nameof(solverMethod));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (degree == 0 && !includeIntercept) {
            throw new ArgumentException(
                "A degree-zero polynomial has no coefficient when the intercept is forbidden.",
                nameof(includeIntercept));
        }

        if (x.Length != y.Length) {
            throw new ArgumentException(
                "The predictor and response lengths must match.",
                nameof(y));
        }

        if (x.IsEmpty) {
            throw new ArgumentException(
                "At least one observation is required.",
                nameof(x));
        }

        if (degree == 0) {
            return FitConstantPolynomial(
                x,
                y,
                solverMethod,
                relativeRankTolerance);
        }

        var predictorCount = degree;
        var predictors = new double[checked(x.Length * predictorCount)];
        for (var row = 0; row < x.Length; row++) {
            if (!double.IsFinite(x[row])) {
                return Failure(
                    RegressionModelKind.Polynomial,
                    solverMethod,
                    RegressionSelectionReason.None,
                    RegressionFitStatus.NonFiniteInput,
                    includeIntercept,
                    predictorCount,
                    predictorCount + (includeIntercept ? 1 : 0),
                    new double[predictorCount + (includeIntercept ? 1 : 0)],
                    x.Length);
            }

            var power = x[row];
            for (var column = 0; column < predictorCount; column++) {
                predictors[(row * predictorCount) + column] = power;
                power *= x[row];
            }
        }

        return FitRegression(
            predictors,
            x.Length,
            predictorCount,
            y,
            RegressionModelKind.Polynomial,
            includeIntercept,
            ridgePenalty: 0,
            solverMethod,
            relativeRankTolerance);
    }

    private static RegressionFitResult FitConstantPolynomial(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        RegressionSolverMethod solverMethod,
        double relativeRankTolerance) {
        for (var index = 0; index < x.Length; index++) {
            if (!double.IsFinite(x[index]) || !double.IsFinite(y[index])) {
                return Failure(
                    RegressionModelKind.Polynomial,
                    solverMethod,
                    RegressionSelectionReason.None,
                    RegressionFitStatus.NonFiniteInput,
                    hasIntercept: true,
                    predictorCount: 0,
                    designColumnCount: 1,
                    new double[1],
                    x.Length);
            }
        }

        var resolvedSolver = solverMethod == RegressionSolverMethod.Auto
            ? RegressionSolverMethod.Qr
            : solverMethod;
        var design = new double[x.Length];
        design.AsSpan().Fill(1);
        var coefficients = new double[1];
        var succeeded = DenseLinearSystemSolver.TrySolve(
            design,
            x.Length,
            1,
            y,
            coefficients,
            MatrixStructure.None,
            out var linearReport,
            resolvedSolver == RegressionSolverMethod.Qr
                ? LinearSolverMethod.Qr
                : LinearSolverMethod.Svd,
            relativeRankTolerance);
        if (!succeeded) {
            return Failure(
                RegressionModelKind.Polynomial,
                LinearSolverMethodToRegressionSolver(linearReport.Method),
                RegressionSelectionReason.None,
                LinearSolveStatusToRegressionStatus(linearReport.Status),
                hasIntercept: true,
                predictorCount: 0,
                designColumnCount: 1,
                new double[1],
                x.Length,
                linearReport.NumericalRank);
        }

        var actualSolver = LinearSolverMethodToRegressionSolver(
            linearReport.Method);
        var mean = Mean(y);
        var residualSumOfSquares = 0.0;
        var totalSumOfSquares = 0.0;
        for (var index = 0; index < y.Length; index++) {
            var residual = y[index] - coefficients[0];
            var centered = y[index] - mean;
            residualSumOfSquares += residual * residual;
            totalSumOfSquares += centered * centered;
        }

        var residualDegreesOfFreedom = x.Length - 1;
        var totalDegreesOfFreedom = x.Length - 1;
        var rSquared = totalSumOfSquares == 0
            ? (residualSumOfSquares == 0 ? 1 : double.NaN)
            : 1 - (residualSumOfSquares / totalSumOfSquares);
        var adjustedRSquared = residualDegreesOfFreedom == 0 ||
            double.IsNaN(rSquared)
            ? double.NaN
            : rSquared;
        var report = new RegressionFitReport(
            RegressionModelKind.Polynomial,
            actualSolver,
            RegressionSelectionReason.FullColumnRank,
            RegressionFitStatus.Succeeded,
            numericalRank: 1,
            observationCount: x.Length,
            designColumnCount: 1,
            totalDegreesOfFreedom,
            residualDegreesOfFreedom,
            residualSumOfSquares,
            totalSumOfSquares,
            rSquared,
            adjustedRSquared);
        return new RegressionFitResult(
            coefficients,
            hasIntercept: true,
            predictorCount: 0,
            report);
    }

    private static RegressionSolverMethod LinearSolverMethodToRegressionSolver(
        LinearSolverMethod method) => method == LinearSolverMethod.Qr
            ? RegressionSolverMethod.Qr
            : RegressionSolverMethod.Svd;

    private static RegressionFitStatus LinearSolveStatusToRegressionStatus(
        LinearSolveStatus status) => status switch {
            LinearSolveStatus.Succeeded => RegressionFitStatus.Succeeded,
            LinearSolveStatus.Singular or LinearSolveStatus.RankDeficient =>
                RegressionFitStatus.RankDeficient,
            LinearSolveStatus.NonFiniteInput =>
                RegressionFitStatus.NonFiniteInput,
            LinearSolveStatus.Unsupported or LinearSolveStatus.Underdetermined =>
                RegressionFitStatus.Unsupported,
            LinearSolveStatus.NonFiniteOutput or
                LinearSolveStatus.NotPositiveDefinite or
                LinearSolveStatus.NumericalFailure =>
                RegressionFitStatus.NumericalFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown linear solve status."),
        };

    private static RegressionFitResult Failure(
        RegressionModelKind modelKind,
        RegressionSolverMethod solverMethod,
        RegressionSelectionReason selectionReason,
        RegressionFitStatus status,
        bool hasIntercept,
        int predictorCount,
        int designColumnCount,
        double[] coefficients,
        int observationCount,
        int numericalRank = 0) {
        var report = new RegressionFitReport(
            modelKind,
            solverMethod,
            selectionReason,
            status,
            numericalRank,
            observationCount,
            designColumnCount,
            0,
            0,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN);
        return new RegressionFitResult(
            coefficients,
            hasIntercept,
            predictorCount,
            report);
    }
}
