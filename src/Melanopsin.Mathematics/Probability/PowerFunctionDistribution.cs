// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A power-function distribution on a finite interval.
/// </summary>
/// <remarks>
/// The density is <c>shape * (x - minimum)^(shape - 1) / width^shape</c>.
/// Shape one is the uniform distribution. Shape values below one place an
/// integrable density singularity at the lower endpoint.
/// </remarks>
public readonly record struct PowerFunctionDistribution :
    IProbabilityDistribution {
    public PowerFunctionDistribution(
        double minimum,
        double maximum,
        double shape) {
        DistributionValidation.Location(minimum, nameof(minimum));
        DistributionValidation.Location(maximum, nameof(maximum));
        if (maximum <= minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "The maximum must be greater than the minimum.");
        }

        DistributionValidation.Scale(shape, nameof(shape));
        Minimum = minimum;
        Maximum = maximum;
        Shape = shape;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Shape { get; }

    public double Mean =>
        Minimum + ((Maximum - Minimum) * Shape / (Shape + 1));

    public double Variance {
        get {
            var width = Maximum - Minimum;
            return width * width * Shape /
                ((Shape + 1) * (Shape + 1) * (Shape + 2));
        }
    }

    /// <summary>
    /// Gets the differential entropy in nats.
    /// </summary>
    public double Entropy =>
        Math.Log((Maximum - Minimum) / Shape) + 1 - (1 / Shape);

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < Minimum || value > Maximum) {
            return 0;
        }

        var width = Maximum - Minimum;
        if (value == Minimum) {
            return Shape < 1
                ? double.PositiveInfinity
                : Shape == 1 ? 1 / width : 0;
        }

        if (value == Maximum) {
            return Shape / width;
        }

        var standardized = (value - Minimum) / width;
        return (Shape / width) * Math.Pow(standardized, Shape - 1);
    }

    public double LogPdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < Minimum || value > Maximum) {
            return double.NegativeInfinity;
        }

        if (value == Minimum) {
            return Shape < 1
                ? double.PositiveInfinity
                : Shape == 1 ? -Math.Log(Maximum - Minimum) : double.NegativeInfinity;
        }

        var standardized = (value - Minimum) / (Maximum - Minimum);
        return Math.Log(Shape / (Maximum - Minimum)) +
            ((Shape - 1) * Math.Log(standardized));
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        return Math.Pow((value - Minimum) / (Maximum - Minimum), Shape);
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= Minimum) {
            return 1;
        }

        if (value >= Maximum) {
            return 0;
        }

        return -FloatingPointMath.Expm1(
            Shape * FloatingPointMath.Log1p(
                -(value - Minimum) / (Maximum - Minimum)));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Minimum;
        }

        if (probability == 1) {
            return Maximum;
        }

        return Minimum +
            ((Maximum - Minimum) * Math.Pow(probability, 1 / Shape));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
