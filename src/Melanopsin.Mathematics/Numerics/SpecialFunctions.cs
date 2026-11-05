// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Double-precision special functions used by probability, statistics, and
/// scientific algorithms.
/// </summary>
/// <remarks>
/// The incomplete gamma and beta implementations use their regularized forms,
/// so their values are dimensionless and avoid separate normalization factors
/// at call sites. Inputs outside a function's real domain return NaN or throw
/// according to the documented method contract. Function definitions,
/// analytic continuations, and asymptotic expansions follow the NIST Digital
/// Library of Mathematical Functions, <see href="https://dlmf.nist.gov/">https://dlmf.nist.gov/</see>.
/// </remarks>
public static partial class SpecialFunctions {
    private const double Tiny = 1e-300;
    private const double RelativeAccuracy = 1e-15;
    private const int MaximumIterations = 10_000;
    private static readonly double[] LanczosCoefficients = [
        0.99999999999980993,
        676.5203681218851,
        -1259.1392167224028,
        771.32342877765313,
        -176.61502916214059,
        12.507343278686905,
        -0.13857109526572012,
        9.9843695780195716e-6,
        1.5056327351493116e-7,
    ];

    /// <summary>
    /// Computes the gamma function for a real argument. Gamma poles return
    /// NaN rather than an unsigned infinity because the residue sign is not
    /// meaningful at an exact pole.
    /// </summary>
    public static double Gamma(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return double.PositiveInfinity;
        }

        if (value <= 0 && value == Math.Truncate(value)) {
            return double.NaN;
        }

        if (value < 0.5) {
            return Constants.Pi /
                (Math.Sin(Constants.Pi * value) * Gamma(1 - value));
        }

        return Math.Exp(LogGamma(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the gamma function for a strictly
    /// positive argument.
    /// </summary>
    public static double LogGamma(double value) {
        if (!(value > 0) || double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return double.PositiveInfinity;
        }

        if (value < 0.5) {
            return Math.Log(Constants.Pi) -
                Math.Log(Math.Sin(Constants.Pi * value)) -
                LogGamma(1 - value);
        }

        var shifted = value - 1;
        var sum = LanczosCoefficients[0];
        for (var index = 1; index < LanczosCoefficients.Length; index++) {
            sum += LanczosCoefficients[index] / (shifted + index);
        }

        var scaled = shifted + 7.5;
        return Constants.Ln2PiOver2 +
            ((shifted + 0.5) * Math.Log(scaled)) -
            scaled +
            Math.Log(sum);
    }

    /// <summary>
    /// Computes the beta function for positive arguments.
    /// </summary>
    public static double Beta(double first, double second) {
        if (!(first > 0) || !(second > 0) ||
            !double.IsFinite(first) || !double.IsFinite(second)) {
            return double.NaN;
        }

        return Math.Exp(LogBeta(first, second));
    }

    /// <summary>
    /// Computes the logarithm of the beta function for positive arguments.
    /// </summary>
    public static double LogBeta(double first, double second) {
        if (!(first > 0) || !(second > 0) ||
            !double.IsFinite(first) || !double.IsFinite(second)) {
            return double.NaN;
        }

        return LogGamma(first) + LogGamma(second) - LogGamma(first + second);
    }

    public static double Erf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        if (double.IsNegativeInfinity(value)) {
            return -1;
        }

        if (value == 0) {
            return value;
        }

        var magnitude = IncompleteGammaRegularized(0.5, value * value);
        return value < 0 ? -magnitude : magnitude;
    }

    public static double Erfc(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (double.IsNegativeInfinity(value)) {
            return 2;
        }

        if (value >= 0) {
            return IncompleteGammaRegularizedComplement(0.5, value * value);
        }

        return 1 + IncompleteGammaRegularized(0.5, value * value);
    }

    /// <summary>
    /// Computes the inverse error function on the closed interval [-1, 1].
    /// </summary>
    /// <remarks>
    /// The implementation evaluates the smaller normal tail and does not
    /// compute <c>1 - probability</c> when that subtraction would discard
    /// significant bits. Endpoints map to signed infinity.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="probability"/> is outside [-1, 1] or NaN.
    /// </exception>
    public static double InverseErf(double probability) {
        if (!double.IsFinite(probability) || probability < -1 || probability > 1) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        if (probability == 0) {
            return 0;
        }

        if (Math.Abs(probability) == 1) {
            return Math.CopySign(double.PositiveInfinity, probability);
        }

        if (probability > 0) {
            var tail = 0.5 * (1 - probability);
            return -InverseNormalCdf(tail) * Constants.InverseSqrt2;
        }

        var lowerTail = 0.5 * (1 + probability);
        return InverseNormalCdf(lowerTail) * Constants.InverseSqrt2;
    }

    /// <summary>
    /// Computes the inverse complementary error function on [0, 2].
    /// </summary>
    /// <remarks>
    /// The lower and upper tails are evaluated through the corresponding
    /// smaller normal tail, avoiding cancellation for very small probabilities.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="probability"/> is outside [0, 2] or NaN.
    /// </exception>
    public static double InverseErfc(double probability) {
        if (!double.IsFinite(probability) || probability < 0 || probability > 2) {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        if (probability == 1) {
            return 0;
        }

        if (probability > 1) {
            return -InverseErfc(2 - probability);
        }

        return -InverseNormalCdf(0.5 * probability) * Constants.InverseSqrt2;
    }

    /// <summary>
    /// Computes the regularized lower incomplete gamma function
    /// <c>P(a, x) = gamma(a, x) / Gamma(a)</c>.
    /// </summary>
    public static double IncompleteGammaRegularized(double shape, double value) {
        if (!(shape > 0) || !double.IsFinite(shape) || value < 0 ||
            double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return 0;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        var logarithm = (shape * Math.Log(value)) - value - LogGamma(shape);
        if (value < shape + 1) {
            var term = 1 / shape;
            var sum = term;
            var denominator = shape;
            for (var iteration = 0; iteration < MaximumIterations; iteration++) {
                denominator++;
                term *= value / denominator;
                sum += term;
                if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                    return Math.Clamp(sum * Math.Exp(logarithm), 0, 1);
                }
            }
        } else {
            return 1 - ContinuedFractionGammaQ(shape, value, logarithm);
        }

        throw new ArithmeticException(
            "The incomplete gamma series did not converge.");
    }

    /// <summary>
    /// Computes the regularized upper incomplete gamma function
    /// <c>Q(a, x) = 1 - P(a, x)</c>.
    /// </summary>
    public static double IncompleteGammaRegularizedComplement(
        double shape,
        double value) {
        if (!(shape > 0) || !double.IsFinite(shape) || value < 0 ||
            double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return 1;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        var logarithm = (shape * Math.Log(value)) - value - LogGamma(shape);
        if (value < shape + 1) {
            return 1 - IncompleteGammaRegularized(shape, value);
        }

        return ContinuedFractionGammaQ(shape, value, logarithm);
    }

    /// <summary>
    /// Computes the regularized incomplete beta function
    /// <c>I_x(a, b)</c>.
    /// </summary>
    public static double IncompleteBetaRegularized(
        double firstShape,
        double secondShape,
        double value) {
        if (!(firstShape > 0) || !(secondShape > 0) ||
            !double.IsFinite(firstShape) || !double.IsFinite(secondShape) ||
            !double.IsFinite(value) || value < 0 || value > 1) {
            return double.NaN;
        }

        if (value == 0) {
            return 0;
        }

        if (value == 1) {
            return 1;
        }

        var logarithm = (firstShape * Math.Log(value)) +
            (secondShape * Math.Log(1 - value)) -
            LogBeta(firstShape, secondShape);
        if (value < (firstShape + 1) / (firstShape + secondShape + 2)) {
            return Math.Clamp(
                Math.Exp(logarithm) *
                    ContinuedFractionBeta(firstShape, secondShape, value) /
                    firstShape,
                0,
                1);
        }

        return Math.Clamp(
            1 - (Math.Exp(logarithm) *
                ContinuedFractionBeta(secondShape, firstShape, 1 - value) /
                secondShape),
            0,
            1);
    }

    /// <summary>
    /// Computes the principal branch of the Lambert W function for
    /// <c>value &gt;= -1/e</c>.
    /// </summary>
    public static double LambertW(double value) {
        if (double.IsNaN(value) || value < -1 / Constants.E ||
            double.IsPositiveInfinity(value)) {
            return double.NaN;
        }

        if (value == -1 / Constants.E) {
            return -1;
        }

        if (value == 0) {
            return 0;
        }

        var estimate = value < -0.25
            ? BranchPointEstimate(value)
            : value <= 1
                ? value
                : Math.Log(value) - Math.Log(Math.Log(value));
        for (var iteration = 0; iteration < 20; iteration++) {
            var exponential = Math.Exp(estimate);
            var difference = (estimate * exponential) - value;
            var denominator = exponential * (estimate + 1) -
                ((estimate + 2) * difference / (2 * estimate + 2));
            var next = estimate - (difference / denominator);
            if (!double.IsFinite(next)) {
                break;
            }

            if (Math.Abs(next - estimate) <=
                4 * ScalarMath.MachineEpsilon * Math.Abs(next)) {
                return next;
            }

            estimate = next;
        }

        return estimate;
    }

    /// <summary>
    /// Computes the digamma function for arguments that are not non-positive
    /// integers.
    /// </summary>
    public static double Digamma(double value) {
        if (double.IsNaN(value) ||
            (value <= 0 && value == Math.Truncate(value))) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return double.PositiveInfinity;
        }

        if (value < 0) {
            return Digamma(1 - value) - (Constants.Pi / Math.Tan(Constants.Pi * value));
        }

        var result = 0.0;
        while (value < 16) {
            result -= 1 / value;
            value++;
        }

        var inverse = 1 / value;
        var inverseSquared = inverse * inverse;
        var inverseFourth = inverseSquared * inverseSquared;
        var inverseSixth = inverseFourth * inverseSquared;
        return result +
            Math.Log(value) -
            (0.5 * inverse) -
            (inverseSquared / 12) +
            (inverseFourth / 120) -
            (inverseSixth / 252) +
            (inverseFourth * inverseFourth / 240) -
            (inverseSixth * inverseFourth / 132) +
            (inverseSixth * inverseSixth * 691 / 32760);
    }

    /// <summary>
    /// Computes the trigamma function for positive arguments.
    /// </summary>
    public static double Trigamma(double value) {
        if (!(value > 0) || double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        var result = 0.0;
        while (value < 16) {
            result += 1 / (value * value);
            value++;
        }

        var inverse = 1 / value;
        var inverseSquared = inverse * inverse;
        var inverseCubed = inverseSquared * inverse;
        return result +
            inverse +
            (inverseSquared / 2) +
            (inverseCubed / 6) -
            (inverseCubed * inverseSquared / 30) +
            (inverseCubed * inverseSquared * inverseSquared / 42) -
            (inverseCubed * inverseSquared * inverseSquared * inverseSquared / 30) +
            (inverseCubed * inverseSquared * inverseSquared * inverseSquared * inverseSquared * 5 / 66);
    }

    private static double ContinuedFractionGammaQ(
        double shape,
        double value,
        double logarithm) {
        var b = value + 1 - shape;
        var c = 1 / Tiny;
        var d = 1 / Math.Max(Math.Abs(b), Tiny) * Math.Sign(b == 0 ? 1 : b);
        var result = d;
        for (var iteration = 1; iteration <= MaximumIterations; iteration++) {
            var coefficient = -iteration * (iteration - shape);
            b += 2;
            d = (coefficient * d) + b;
            if (Math.Abs(d) < Tiny) {
                d = Tiny;
            }

            c = b + (coefficient / c);
            if (Math.Abs(c) < Tiny) {
                c = Tiny;
            }

            d = 1 / d;
            var delta = d * c;
            result *= delta;
            if (Math.Abs(delta - 1) <= RelativeAccuracy) {
                return Math.Clamp(Math.Exp(logarithm) * result, 0, 1);
            }
        }

        throw new ArithmeticException(
            "The incomplete gamma continued fraction did not converge.");
    }

    private static double ContinuedFractionBeta(
        double firstShape,
        double secondShape,
        double value) {
        var sum = firstShape + secondShape;
        var firstPlusOne = firstShape + 1;
        var firstMinusOne = firstShape - 1;
        var c = 1.0;
        var d = 1 - (sum * value / firstPlusOne);
        if (Math.Abs(d) < Tiny) {
            d = Tiny;
        }

        d = 1 / d;
        var result = d;
        for (var iteration = 1; iteration <= MaximumIterations; iteration++) {
            var doubled = 2 * iteration;
            var coefficient = iteration * (secondShape - iteration) * value /
                ((firstMinusOne + doubled) * (firstShape + doubled));
            d = 1 + (coefficient * d);
            if (Math.Abs(d) < Tiny) {
                d = Tiny;
            }

            c = 1 + (coefficient / c);
            if (Math.Abs(c) < Tiny) {
                c = Tiny;
            }

            d = 1 / d;
            result *= d * c;
            coefficient = -(firstShape + iteration) * (sum + iteration) * value /
                ((firstShape + doubled) * (firstPlusOne + doubled));
            d = 1 + (coefficient * d);
            if (Math.Abs(d) < Tiny) {
                d = Tiny;
            }

            c = 1 + (coefficient / c);
            if (Math.Abs(c) < Tiny) {
                c = Tiny;
            }

            d = 1 / d;
            var delta = d * c;
            result *= delta;
            if (Math.Abs(delta - 1) <= RelativeAccuracy) {
                return result;
            }
        }

        throw new ArithmeticException(
            "The incomplete beta continued fraction did not converge.");
    }

    private static double BranchPointEstimate(double value) {
        var offset = Math.Sqrt(2 * ((Constants.E * value) + 1));
        return -1 + offset - (offset * offset / 6) +
            (offset * offset * offset / 72) -
            (offset * offset * offset * offset / 270);
    }
}
