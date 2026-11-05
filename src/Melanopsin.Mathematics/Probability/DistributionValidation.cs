// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

internal static class DistributionValidation {
    public static void Scale(double value, string parameterName) {
        if (!double.IsFinite(value) || value <= 0) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public static void Location(double value, string parameterName) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public static void Probability(double probability) {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }
    }

    public static double FindQuantileOnPositiveSupport(
        Func<double, double> cumulative,
        double probability,
        double initialUpper) {
        Probability(probability);
        if (probability == 0) {
            return 0;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        if (!(initialUpper > 0) || !double.IsFinite(initialUpper)) {
            throw new ArgumentOutOfRangeException(nameof(initialUpper));
        }

        var lower = 0.0;
        var upper = Math.Max(1, initialUpper);
        var width = upper - lower;
        var valueAtUpper = cumulative(upper);
        for (var iteration = 0;
            iteration < 2048 && valueAtUpper < probability;
            iteration++) {
            lower = upper;
            width = Math.Max(width, Math.Abs(upper) * 0.5);
            if (upper > double.MaxValue * 0.5) {
                upper = double.MaxValue;
            } else {
                upper *= 2;
            }

            if (!double.IsFinite(upper) || upper <= lower) {
                throw new ArithmeticException(
                    "A finite quantile could not be bracketed.");
            }

            valueAtUpper = cumulative(upper);
        }

        if (!(valueAtUpper >= probability)) {
            throw new ArithmeticException(
                "A finite quantile could not be bracketed.");
        }

        for (var iteration = 0; iteration < 256; iteration++) {
            var midpoint = lower + ((upper - lower) * 0.5);
            if (midpoint == lower || midpoint == upper) {
                return midpoint;
            }

            var midpointValue = cumulative(midpoint);
            if (midpointValue < probability) {
                lower = midpoint;
            } else {
                upper = midpoint;
            }

            var bracketScale = Math.Max(Math.Abs(lower), Math.Abs(upper));
            if (upper - lower <=
                16 * ScalarMath.MachineEpsilon * bracketScale) {
                return lower + ((upper - lower) * 0.5);
            }
        }

        return lower + ((upper - lower) * 0.5);
    }

    /// <remarks>
    /// Uses the polar-free trigonometric form of the Box-Muller transform from
    /// <see href="https://doi.org/10.1214/aoms/1177706645">Box and Muller (1958)</see>.
    /// </remarks>
    public static double SampleNormal(ref Xoshiro256StarStar generator) {
        var first = 1 - generator.NextDouble();
        var second = generator.NextDouble();
        return Math.Sqrt(-2 * Math.Log(first)) *
            Math.Cos(Constants.TwoPi * second);
    }

    /// <remarks>
    /// For <c>shape &gt;= 1</c> this uses the acceptance-rejection method of
    /// <see href="https://doi.org/10.1145/358407.358414">Marsaglia and Tsang (2000)</see>.
    /// For <c>shape &lt; 1</c> the standard boosting transformation is used.
    /// </remarks>
    public static double SampleStandardGamma(
        ref Xoshiro256StarStar generator,
        double shape) {
        if (shape < 1) {
            var transformed = 1 - generator.NextDouble();
            return SampleStandardGamma(ref generator, shape + 1) *
                Math.Pow(transformed, 1 / shape);
        }

        var d = shape - (1.0 / 3);
        var c = 1 / Math.Sqrt(9 * d);
        while (true) {
            double normal;
            double cube;
            do {
                normal = SampleNormal(ref generator);
                cube = 1 + (c * normal);
            } while (cube <= 0);

            var value = cube * cube * cube;
            var uniform = generator.NextDouble();
            if (uniform < 1 - (0.0331 * normal * normal * normal * normal) ||
                Math.Log(uniform) <
                    (0.5 * normal * normal) + d - (d * value) +
                    (d * Math.Log(value))) {
                return d * value;
            }
        }
    }
}
