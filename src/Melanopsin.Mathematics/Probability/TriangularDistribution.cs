// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A triangular distribution on a finite closed interval.
/// </summary>
/// <remarks>
/// The mode may equal either endpoint. In those cases the density remains
/// integrable and the inverse-CDF quantile formula is evaluated in its
/// limiting form.
/// </remarks>
public readonly record struct TriangularDistribution : IProbabilityDistribution {
    public TriangularDistribution(double minimum, double mode, double maximum) {
        DistributionValidation.Location(minimum, nameof(minimum));
        DistributionValidation.Location(mode, nameof(mode));
        DistributionValidation.Location(maximum, nameof(maximum));
        if (maximum <= minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "The maximum must exceed the minimum.");
        }

        if (mode < minimum || mode > maximum) {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                "The mode must lie within the closed interval.");
        }

        Minimum = minimum;
        Mode = mode;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Mode { get; }

    public double Maximum { get; }

    public double Mean => (Minimum + Mode + Maximum) / 3;

    public double Variance {
        get {
            var minimumSquared = Minimum * Minimum;
            var modeSquared = Mode * Mode;
            var maximumSquared = Maximum * Maximum;
            return (minimumSquared + modeSquared + maximumSquared -
                (Minimum * Mode) -
                (Minimum * Maximum) -
                (Mode * Maximum)) / 18;
        }
    }

    public double Pdf(double value) {
        if (value < Minimum || value > Maximum || !double.IsFinite(value)) {
            return 0;
        }

        if (value < Mode) {
            return 2 * (value - Minimum) /
                ((Maximum - Minimum) * (Mode - Minimum));
        }

        if (value > Mode) {
            return 2 * (Maximum - value) /
                ((Maximum - Minimum) * (Maximum - Mode));
        }

        return Mode == Minimum
            ? 2 / (Maximum - Minimum)
            : Mode == Maximum
                ? 2 / (Maximum - Minimum)
                : 2 / (Maximum - Minimum);
    }

    public double Cdf(double value) {
        if (value <= Minimum) {
            return 0;
        }

        if (value >= Maximum) {
            return 1;
        }

        if (value < Mode) {
            var lowerWidth = value - Minimum;
            return lowerWidth * lowerWidth /
                ((Maximum - Minimum) * (Mode - Minimum));
        }

        var upperWidth = Maximum - value;
        return 1 - (upperWidth * upperWidth /
            ((Maximum - Minimum) * (Maximum - Mode)));
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return Minimum;
        }

        if (probability == 1) {
            return Maximum;
        }

        var modeProbability = (Mode - Minimum) / (Maximum - Minimum);
        if (probability < modeProbability) {
            return Minimum + Math.Sqrt(
                probability *
                (Maximum - Minimum) *
                (Mode - Minimum));
        }

        return Maximum - Math.Sqrt(
            (1 - probability) *
            (Maximum - Minimum) *
            (Maximum - Mode));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }
}
