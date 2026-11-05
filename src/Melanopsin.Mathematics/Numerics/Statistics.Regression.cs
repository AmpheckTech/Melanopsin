// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The result of an ordinary least-squares simple linear regression.
/// </summary>
public readonly record struct SimpleLinearRegressionResult {
    public SimpleLinearRegressionResult(
        double slope,
        double intercept,
        double rSquared,
        double residualSumOfSquares,
        int count) {
        Slope = slope;
        Intercept = intercept;
        RSquared = rSquared;
        ResidualSumOfSquares = residualSumOfSquares;
        Count = count;
    }

    public double Slope { get; }

    public double Intercept { get; }

    /// <summary>
    /// Gets the coefficient of determination. It is one when the response is
    /// exactly constant and the fitted residual is zero.
    /// </summary>
    public double RSquared { get; }

    public double ResidualSumOfSquares { get; }

    public int Count { get; }

    public double Predict(double x) => (Slope * x) + Intercept;
}

public static partial class Statistics {
    /// <summary>
    /// Fits <c>y = slope * x + intercept</c> by ordinary least squares.
    /// </summary>
    /// <remarks>
    /// The calculation uses a centered single-pass covariance. It throws when
    /// the predictor has exactly zero variance, but it does not reject a
    /// near-zero predictor variance on an absolute scale.
    /// </remarks>
    public static SimpleLinearRegressionResult SimpleLinearRegression(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        ValidateCovarianceInputs(x, y);
        if (x.Length < 2) {
            throw new ArgumentException(
                "At least two paired values are required.",
                nameof(x));
        }

        var meanX = 0.0;
        var meanY = 0.0;
        var sumSquaresX = 0.0;
        var sumProducts = 0.0;
        for (var index = 0; index < x.Length; index++) {
            var count = index + 1;
            var deltaX = x[index] - meanX;
            var deltaY = y[index] - meanY;
            meanX += deltaX / count;
            meanY += deltaY / count;
            sumSquaresX += deltaX * (x[index] - meanX);
            sumProducts += deltaX * (y[index] - meanY);
        }

        if (sumSquaresX == 0) {
            throw new ArgumentException(
                "A simple linear regression requires a nonconstant predictor.",
                nameof(x));
        }

        var slope = sumProducts / sumSquaresX;
        var intercept = meanY - (slope * meanX);
        var residualSumOfSquares = 0.0;
        var totalSumOfSquares = 0.0;
        for (var index = 0; index < x.Length; index++) {
            var residual = y[index] - ((slope * x[index]) + intercept);
            residualSumOfSquares += residual * residual;
            var centered = y[index] - meanY;
            totalSumOfSquares += centered * centered;
        }

        var rSquared = totalSumOfSquares == 0
            ? (residualSumOfSquares == 0 ? 1 : double.NaN)
            : 1 - (residualSumOfSquares / totalSumOfSquares);
        return new SimpleLinearRegressionResult(
            slope,
            intercept,
            rSquared,
            residualSumOfSquares,
            x.Length);
    }
}
