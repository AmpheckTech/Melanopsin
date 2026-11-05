namespace Melanopsin.Mathematics;

/// <summary>
/// The result of an ordinary least-squares multiple linear regression.
/// </summary>
/// <remarks>
/// Coefficients are stored in model order. When an intercept is present, it
/// is the first coefficient and is followed by one coefficient per predictor.
/// Standard errors use the residual-mean-square estimate and are NaN when no
/// residual degrees of freedom remain.
/// </remarks>
public sealed class MultipleLinearRegressionResult {
    private readonly double[] _coefficients;
    private readonly double[] _coefficientStandardErrors;

    internal MultipleLinearRegressionResult(
        double[] coefficients,
        double[] coefficientStandardErrors,
        bool hasIntercept,
        int predictorCount,
        int count,
        double residualSumOfSquares,
        double totalSumOfSquares,
        double rSquared,
        double adjustedRSquared) {
        _coefficients = coefficients;
        _coefficientStandardErrors = coefficientStandardErrors;
        HasIntercept = hasIntercept;
        PredictorCount = predictorCount;
        Count = count;
        ResidualSumOfSquares = residualSumOfSquares;
        TotalSumOfSquares = totalSumOfSquares;
        RSquared = rSquared;
        AdjustedRSquared = adjustedRSquared;
    }

    /// <summary>
    /// Gets whether the first coefficient is an intercept.
    /// </summary>
    public bool HasIntercept { get; }

    /// <summary>
    /// Gets the number of predictor columns, excluding any intercept.
    /// </summary>
    public int PredictorCount { get; }

    /// <summary>
    /// Gets the number of observations.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the estimated coefficients in model order.
    /// </summary>
    public ReadOnlySpan<double> Coefficients => _coefficients;

    /// <summary>
    /// Gets the coefficient standard errors in model order.
    /// </summary>
    public ReadOnlySpan<double> CoefficientStandardErrors =>
        _coefficientStandardErrors;

    public double ResidualSumOfSquares { get; }

    public double TotalSumOfSquares { get; }

    /// <summary>
    /// Gets the coefficient of determination. A constant response with zero
    /// residual is assigned <c>1</c>; a constant response with nonzero
    /// residual is assigned NaN.
    /// </summary>
    public double RSquared { get; }

    /// <summary>
    /// Gets the degrees-of-freedom-adjusted coefficient of determination. It
    /// is NaN when the model consumes all observations.
    /// </summary>
    public double AdjustedRSquared { get; }

    /// <summary>
    /// Gets the residual standard error, or NaN when no residual degrees of
    /// freedom remain.
    /// </summary>
    public double ResidualStandardError =>
        Count == Coefficients.Length
            ? double.NaN
            : Math.Sqrt(ResidualSumOfSquares / (Count - Coefficients.Length));

    /// <summary>
    /// Copies the coefficients into a caller-provided destination.
    /// </summary>
    public void CopyCoefficientsTo(Span<double> destination) {
        if (destination.Length < _coefficients.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        _coefficients.CopyTo(destination);
    }

    /// <summary>
    /// Evaluates the fitted model at one predictor vector.
    /// </summary>
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

public static partial class Statistics {
    /// <summary>
    /// Fits a multiple linear model by Householder QR least squares.
    /// </summary>
    /// <param name="predictors">
    /// Row-major predictor storage containing at least
    /// <c>rowCount * predictorCount</c> values.
    /// </param>
    /// <param name="rowCount">The number of observations.</param>
    /// <param name="predictorCount">
    /// The number of predictor columns, excluding any intercept.
    /// </param>
    /// <param name="response">At least <paramref name="rowCount"/> values.</param>
    /// <param name="includeIntercept">
    /// Whether to prepend a column of ones and report an intercept as the first
    /// coefficient.
    /// </param>
    /// <remarks>
    /// The model is full-column-rank only when every diagonal entry of its QR
    /// factorization is exactly nonzero. No absolute near-zero pivot threshold
    /// is applied. For numerical-rank decisions or near-collinear diagnostics,
    /// inspect singular values through <see cref="SvdDecomposition"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The dimensions are inconsistent, the model is exactly rank deficient,
    /// or an input value is non-finite.
    /// </exception>
    public static MultipleLinearRegressionResult MultipleLinearRegression(
        ReadOnlySpan<double> predictors,
        int rowCount,
        int predictorCount,
        ReadOnlySpan<double> response,
        bool includeIntercept = true) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(predictorCount);
        var requiredPredictors = checked(rowCount * predictorCount);
        if (predictors.Length < requiredPredictors) {
            throw new ArgumentException(
                "The predictor storage does not contain enough values.",
                nameof(predictors));
        }

        if (response.Length < rowCount) {
            throw new ArgumentException(
                "The response does not contain enough values.",
                nameof(response));
        }

        var designColumnCount = checked(predictorCount + (includeIntercept ? 1 : 0));
        if (rowCount < designColumnCount) {
            throw new ArgumentException(
                "The model must have at least as many observations as coefficients.",
                nameof(rowCount));
        }

        var design = new double[checked(rowCount * designColumnCount)];
        for (var row = 0; row < rowCount; row++) {
            var designOffset = row * designColumnCount;
            var predictorOffset = row * predictorCount;
            var designColumn = 0;
            if (includeIntercept) {
                design[designOffset + designColumn] = 1;
                designColumn++;
            }

            for (var column = 0; column < predictorCount; column++) {
                var value = predictors[predictorOffset + column];
                if (!double.IsFinite(value)) {
                    throw new ArgumentException(
                        "Predictors must contain only finite values.",
                        nameof(predictors));
                }

                design[designOffset + designColumn + column] = value;
            }

            if (!double.IsFinite(response[row])) {
                throw new ArgumentException(
                    "The response must contain only finite values.",
                    nameof(response));
            }
        }

        var qr = new QrDecomposition(design, rowCount, designColumnCount);
        if (!qr.IsFullColumnRank) {
            throw new ArgumentException(
                "The design matrix is exactly rank deficient.",
                nameof(predictors));
        }

        var coefficients = qr.LeastSquares(response[..rowCount]);
        var mean = Mean(response[..rowCount]);
        var residualSumOfSquares = 0.0;
        var totalSumOfSquares = 0.0;
        for (var row = 0; row < rowCount; row++) {
            var fitted = 0.0;
            var offset = row * designColumnCount;
            for (var column = 0; column < designColumnCount; column++) {
                fitted += design[offset + column] * coefficients[column];
            }

            var residual = response[row] - fitted;
            residualSumOfSquares += residual * residual;
            var centered = response[row] - mean;
            totalSumOfSquares += centered * centered;
        }

        var rSquared = totalSumOfSquares == 0
            ? (residualSumOfSquares == 0 ? 1 : double.NaN)
            : 1 - (residualSumOfSquares / totalSumOfSquares);
        var residualDegreesOfFreedom = rowCount - designColumnCount;
        var adjustedRSquared = residualDegreesOfFreedom == 0
            ? double.NaN
            : 1 - ((1 - rSquared) *
                ((rowCount - (includeIntercept ? 1 : 0.0)) /
                    residualDegreesOfFreedom));
        var standardErrors = ComputeCoefficientStandardErrors(
            qr,
            residualSumOfSquares,
            residualDegreesOfFreedom);
        return new MultipleLinearRegressionResult(
            coefficients,
            standardErrors,
            includeIntercept,
            predictorCount,
            rowCount,
            residualSumOfSquares,
            totalSumOfSquares,
            rSquared,
            adjustedRSquared);
    }

    private static double[] ComputeCoefficientStandardErrors(
        QrDecomposition qr,
        double residualSumOfSquares,
        int residualDegreesOfFreedom) {
        var coefficients = qr.Columns;
        var standardErrors = new double[coefficients];
        if (residualDegreesOfFreedom == 0) {
            Array.Fill(standardErrors, double.NaN);
            return standardErrors;
        }

        var r = qr.GetR();
        var variance = residualSumOfSquares / residualDegreesOfFreedom;
        var intermediate = new double[coefficients];
        var solution = new double[coefficients];
        for (var target = 0; target < coefficients; target++) {
            for (var row = 0; row < coefficients; row++) {
                var value = row == target ? 1.0 : 0.0;
                for (var column = 0; column < row; column++) {
                    value -= r[(column * coefficients) + row] *
                        intermediate[column];
                }

                intermediate[row] = value / r[(row * coefficients) + row];
            }

            for (var row = coefficients - 1; row >= 0; row--) {
                var value = intermediate[row];
                for (var column = row + 1; column < coefficients; column++) {
                    value -= r[(row * coefficients) + column] *
                        solution[column];
                }

                solution[row] = value / r[(row * coefficients) + row];
            }

            var diagonal = solution[target] * variance;
            standardErrors[target] = diagonal >= 0 ? Math.Sqrt(diagonal) : double.NaN;
        }

        return standardErrors;
    }
}
