namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the standard normal probability density
    /// <c>exp(-x^2 / 2) / sqrt(2 pi)</c>.
    /// </summary>
    public static double StandardNormalPdf(double value) {
        return Constants.InverseSqrt2Pi * Math.Exp(-0.5 * value * value);
    }

    /// <summary>
    /// Computes the standard normal cumulative distribution function.
    /// </summary>
    public static double NormalCdf(double value) {
        return 0.5 * Erfc(-value * Constants.InverseSqrt2);
    }

    /// <summary>
    /// Computes the standard normal upper-tail probability
    /// <c>P(Z &gt; value)</c>.
    /// </summary>
    public static double NormalSurvival(double value) {
        return 0.5 * Erfc(value * Constants.InverseSqrt2);
    }

    /// <summary>
    /// Computes the natural logarithm of the standard normal cumulative
    /// distribution function without first materializing an underflowing tail.
    /// </summary>
    public static double NormalLogCdf(double value) {
        return LogLowerNormalTail(value);
    }

    /// <summary>
    /// Computes the natural logarithm of the standard normal upper-tail
    /// probability without first materializing an underflowing tail.
    /// </summary>
    public static double NormalLogSurvival(double value) {
        return LogLowerNormalTail(-value);
    }

    /// <summary>
    /// Computes the standard normal quantile from the logarithm of a lower-tail
    /// probability without first materializing the probability.
    /// </summary>
    /// <remarks>
    /// This overload remains accurate for probabilities whose direct double
    /// representation would underflow. A log-probability of zero maps to
    /// positive infinity and negative infinity maps to negative infinity.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="logProbability"/> is NaN or positive.
    /// </exception>
    public static double InverseNormalLogCdf(double logProbability) {
        if (double.IsNaN(logProbability) || logProbability > 0) {
            throw new ArgumentOutOfRangeException(nameof(logProbability));
        }

        if (logProbability == 0) {
            return double.PositiveInfinity;
        }

        if (double.IsNegativeInfinity(logProbability)) {
            return double.NegativeInfinity;
        }

        if (logProbability >= -Constants.Ln2) {
            return InverseNormalCdfComplement(
                -FloatingPointMath.Expm1(logProbability));
        }

        // The limiting tail approximation gives a safe bracket. The normal
        // log-CDF is monotone, so bisection avoids cancellation in 1 - CDF.
        var low = -Math.Sqrt(-2 * logProbability) - 2;
        var high = 0.0;
        for (var iteration = 0; iteration < 80; iteration++) {
            var middle = low + ((high - low) * 0.5);
            if (NormalLogCdf(middle) < logProbability) {
                low = middle;
            } else {
                high = middle;
            }

            if (high - low <= 4 * ScalarMath.MachineEpsilon * Math.Max(Math.Abs(low), Math.Abs(high))) {
                break;
            }
        }

        return low + ((high - low) * 0.5);
    }

    /// <summary>
    /// Computes the standard normal quantile from the logarithm of an upper-tail
    /// probability without first materializing the probability.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="logProbability"/> is NaN or positive.
    /// </exception>
    public static double InverseNormalLogSurvival(double logProbability) {
        return -InverseNormalLogCdf(logProbability);
    }

    /// <summary>
    /// Computes the standard normal quantile selected by an upper-tail
    /// probability, so that <c>P(Z &gt; result) = probability</c>.
    /// </summary>
    /// <remarks>
    /// This avoids the cancellation of constructing <c>1 - probability</c> for
    /// probabilities close to one. The result maps zero to positive infinity
    /// and one to negative infinity.
    /// </remarks>
    public static double InverseNormalCdfComplement(double probability) {
        return -InverseNormalCdf(probability);
    }

    /// <summary>
    /// Computes the standard normal quantile function on the closed interval
    /// [0, 1].
    /// </summary>
    /// <remarks>
    /// The rational approximation is evaluated separately in the lower tail,
    /// central region, and upper tail. Working with the smaller tail avoids
    /// the cancellation that occurs when a tail probability is formed from
    /// <c>1 - probability</c>. Central-region values receive an additional
    /// Halley refinement. Endpoints map to signed infinity.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="probability"/> is outside [0, 1] or NaN.
    /// </exception>
    public static double InverseNormalCdf(double probability) {
        if (!double.IsFinite(probability) ||
            probability < 0 ||
            probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        const double lowerTailThreshold = 0.02425;
        const double upperTailThreshold = 1 - lowerTailThreshold;
        double estimate;
        if (probability < lowerTailThreshold) {
            estimate = EvaluateLowerNormalTail(probability);
        } else if (probability > upperTailThreshold) {
            estimate = -EvaluateLowerNormalTail(1 - probability);
        } else {
            var offset = probability - 0.5;
            var squared = offset * offset;
            estimate = offset *
                ((((((NormalCentralNumerator[0] * squared) +
                    NormalCentralNumerator[1]) * squared +
                    NormalCentralNumerator[2]) * squared +
                    NormalCentralNumerator[3]) * squared +
                    NormalCentralNumerator[4]) * squared +
                    NormalCentralNumerator[5]);
            estimate /=
                (((((NormalCentralDenominator[0] * squared) +
                    NormalCentralDenominator[1]) * squared +
                    NormalCentralDenominator[2]) * squared +
                    NormalCentralDenominator[3]) * squared +
                    NormalCentralDenominator[4]) * squared +
                    1;
        }

        return RefineNormalQuantile(estimate, probability);
    }

    /// <summary>
    /// Computes the scaled complementary error function
    /// <c>exp(x^2) erfc(x)</c> without forming the two factors separately for
    /// non-negative arguments.
    /// </summary>
    /// <remarks>
    /// For positive arguments the evaluation uses a continued fraction. For
    /// negative arguments the defining product is used; its result may
    /// overflow at large negative magnitudes.
    /// </remarks>
    public static double Erfcx(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == double.PositiveInfinity) {
            return 0;
        }

        if (value == double.NegativeInfinity) {
            return double.PositiveInfinity;
        }

        if (value < 0) {
            return Math.Exp(value * value) * Erfc(value);
        }

        if (value < 0.5) {
            return Math.Exp(value * value) * Erfc(value);
        }

        var denominator = value;
        var inverseDenominator = 1 / denominator;
        var numerator = 1 / Tiny;
        var result = inverseDenominator;
        for (var iteration = 1; iteration <= MaximumIterations; iteration++) {
            var coefficient = 0.5 * iteration;
            denominator = value + (coefficient * inverseDenominator);
            if (Math.Abs(denominator) < Tiny) {
                denominator = Tiny;
            }

            numerator = value + (coefficient / numerator);
            if (Math.Abs(numerator) < Tiny) {
                numerator = Tiny;
            }

            inverseDenominator = 1 / denominator;
            var delta = inverseDenominator * numerator;
            result *= delta;
            if (Math.Abs(delta - 1) <= 4 * ScalarMath.MachineEpsilon) {
                return result / Constants.SqrtPi;
            }
        }

        throw new ArithmeticException(
            "The scaled complementary error function did not converge.");
    }

    private static double LogLowerNormalTail(double value) {
        if (value < 0) {
            var scaledTail = Math.Max(
                0,
                0.5 * Erfcx(-value * Constants.InverseSqrt2));
            return scaledTail > 0
                ? (-0.5 * value * value) + Math.Log(scaledTail)
                : double.NegativeInfinity;
        }

        var upperTail = NormalSurvival(value);
        return upperTail >= 1
            ? double.NegativeInfinity
            : FloatingPointMath.Log1p(-upperTail);
    }

    private static double RefineNormalQuantile(
        double estimate,
        double probability) {
        var density = Constants.InverseSqrt2Pi *
            Math.Exp(-0.5 * estimate * estimate);
        if (!(density > 0) || !double.IsFinite(density)) {
            return estimate;
        }

        double error;
        if (probability > 0.5) {
            var upperTail = 1 - probability;
            var computedTail = 0.5 * Erfc(estimate * Constants.InverseSqrt2);
            error = upperTail - computedTail;
        } else {
            var computedCdf = 0.5 * Erfc(-estimate * Constants.InverseSqrt2);
            error = computedCdf - probability;
        }

        var correction = error / density;
        var refined = estimate -
            (correction / (1 + (0.5 * estimate * correction)));
        return double.IsFinite(refined) ? refined : estimate;
    }

    private static double EvaluateLowerNormalTail(double probability) {
        var root = Math.Sqrt(-2 * Math.Log(probability));
        var numerator =
            (((((NormalTailNumerator[0] * root) +
                NormalTailNumerator[1]) * root +
                NormalTailNumerator[2]) * root +
                NormalTailNumerator[3]) * root +
                NormalTailNumerator[4]) * root +
                NormalTailNumerator[5];
        var denominator =
            ((((NormalTailDenominator[0] * root) +
                NormalTailDenominator[1]) * root +
                NormalTailDenominator[2]) * root +
                NormalTailDenominator[3]) * root +
                1;
        return numerator / denominator;
    }

    private static readonly double[] NormalCentralNumerator = [
        -3.969683028665376e+01,
        2.209460984245205e+02,
        -2.759285104469687e+02,
        1.383577518672690e+02,
        -3.066479806614716e+01,
        2.506628277459239e+00,
    ];

    private static readonly double[] NormalCentralDenominator = [
        -5.447609879822406e+01,
        1.615858368580409e+02,
        -1.556989798598866e+02,
        6.680131188771972e+01,
        -1.328068155288572e+01,
    ];

    private static readonly double[] NormalTailNumerator = [
        -7.784894002430293e-03,
        -3.223964580411365e-01,
        -2.400758277161838e+00,
        -2.549732539343734e+00,
        4.374664141464968e+00,
        2.938163982698783e+00,
    ];

    private static readonly double[] NormalTailDenominator = [
        7.784695709041462e-03,
        3.224671290700398e-01,
        2.445134137142996e+00,
        3.754408661907416e+00,
    ];
}
