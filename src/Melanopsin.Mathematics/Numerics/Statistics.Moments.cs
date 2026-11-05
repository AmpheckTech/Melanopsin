namespace Melanopsin.Mathematics;

public static partial class Statistics {
    /// <summary>
    /// Computes the bias-corrected sample skewness.
    /// </summary>
    /// <remarks>
    /// This is the <c>G1</c> estimator, not the uncorrected population
    /// moment ratio. It requires at least three finite observations and a
    /// positive sample variance. A constant sample has undefined skewness and
    /// produces <see cref="double.NaN"/>.
    /// </remarks>
    public static double SkewnessSample(ReadOnlySpan<double> values) {
        if (values.Length < 3) {
            throw new ArgumentException(
                "At least three values are required for sample skewness.",
                nameof(values));
        }

        Validate(values);
        var count = values.Length;
        var mean = Mean(values);
        var second = 0.0;
        var third = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var difference = values[index] - mean;
            var squared = difference * difference;
            second += squared;
            third += squared * difference;
        }

        if (second == 0) {
            return double.NaN;
        }

        var populationSkewness = (third / count) /
            Math.Pow(second / count, 1.5);
        var correction = Math.Sqrt((double)count * (count - 1)) /
            (count - 2);
        return correction * populationSkewness;
    }

    /// <summary>
    /// Computes the bias-corrected sample excess kurtosis.
    /// </summary>
    /// <remarks>
    /// This is the <c>G2</c> estimator with the normal distribution's excess
    /// kurtosis equal to zero. It requires at least four finite observations
    /// and a positive sample variance. A constant sample has undefined
    /// kurtosis and produces <see cref="double.NaN"/>.
    /// </remarks>
    public static double ExcessKurtosisSample(ReadOnlySpan<double> values) {
        if (values.Length < 4) {
            throw new ArgumentException(
                "At least four values are required for sample excess kurtosis.",
                nameof(values));
        }

        Validate(values);
        var count = values.Length;
        var mean = Mean(values);
        var second = 0.0;
        var fourth = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var difference = values[index] - mean;
            var squared = difference * difference;
            second += squared;
            fourth += squared * squared;
        }

        if (second == 0) {
            return double.NaN;
        }

        var populationExcess = (fourth / count) /
            ((second / count) * (second / count)) - 3;
        var numerator = (count - 1.0) *
            (((count + 1.0) * populationExcess) + 6);
        var denominator = (count - 2.0) * (count - 3.0);
        return numerator / denominator;
    }

    /// <summary>
    /// Computes the large-sample standard error of sample skewness.
    /// </summary>
    /// <remarks>
    /// The formula is the conventional normal-approximation standard error
    /// <c>sqrt(6 n (n - 1) / ((n - 2)(n + 1)(n + 3)))</c>. It is not a
    /// confidence interval and does not require the sample values.
    /// </remarks>
    public static double StandardErrorOfSkewness(int count) {
        if (count < 3) {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "At least three observations are required.");
        }

        return Math.Sqrt(
            6.0 * count * (count - 1.0) /
            ((count - 2.0) * (count + 1.0) * (count + 3.0)));
    }

    /// <summary>
    /// Computes the large-sample standard error of sample excess kurtosis.
    /// </summary>
    /// <remarks>
    /// The formula is the conventional normal-approximation standard error
    /// <c>sqrt(24 n (n - 1)^2 / ((n - 3)(n - 2)(n + 3)(n + 5)))</c>. It is
    /// not a confidence interval and does not require the sample values.
    /// </remarks>
    public static double StandardErrorOfExcessKurtosis(int count) {
        if (count < 4) {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "At least four observations are required.");
        }

        var countMinusOne = count - 1.0;
        return Math.Sqrt(
            24.0 * count * countMinusOne * countMinusOne /
            ((count - 3.0) * (count - 2.0) * (count + 3.0) * (count + 5.0)));
    }

    /// <summary>
    /// Computes the geometric mean of non-negative finite values.
    /// </summary>
    /// <remarks>
    /// Any zero makes the result zero. Negative values or an empty sample are
    /// rejected. Logarithms are accumulated to avoid intermediate overflow for
    /// widely scaled positive inputs.
    /// </remarks>
    public static double GeometricMean(ReadOnlySpan<double> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var logarithmSum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            if (value < 0) {
                throw new ArgumentException(
                    "Geometric mean requires non-negative values.",
                    nameof(values));
            }

            if (value == 0) {
                return 0;
            }

            logarithmSum += Math.Log(value);
        }

        return Math.Exp(logarithmSum / values.Length);
    }

    /// <summary>
    /// Computes the harmonic mean of positive finite values.
    /// </summary>
    /// <remarks>
    /// Reciprocal summation is scaled by the largest value so values close to
    /// the floating-point range are handled without avoidable overflow.
    /// </remarks>
    public static double HarmonicMean(ReadOnlySpan<double> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var maximum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            if (value <= 0) {
                throw new ArgumentException(
                    "Harmonic mean requires positive values.",
                    nameof(values));
            }

            maximum = Math.Max(maximum, value);
        }

        var reciprocalSum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            reciprocalSum += maximum / values[index];
        }

        return maximum * values.Length / reciprocalSum;
    }

    /// <summary>
    /// Computes the root mean square of finite values.
    /// </summary>
    public static double RootMeanSquare(ReadOnlySpan<double> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        Validate(values);
        var scale = 0.0;
        var scaledSquareSum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var absolute = Math.Abs(values[index]);
            if (absolute == 0) {
                continue;
            }

            if (scale < absolute) {
                var ratio = scale / absolute;
                scaledSquareSum = 1 + (scaledSquareSum * ratio * ratio);
                scale = absolute;
            } else {
                var ratio = absolute / scale;
                scaledSquareSum += ratio * ratio;
            }
        }

        return scale == 0
            ? 0
            : scale * Math.Sqrt(scaledSquareSum / values.Length);
    }
}
