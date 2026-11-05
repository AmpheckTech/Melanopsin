namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private const double AirySeriesBoundary = 8;
    private const double AiryFirstZero = 0.3550280538878172392600631860041831764;
    private const double AiryFirstDerivativeZero = 0.2588194037928067984051835601892039635;

    /// <summary>
    /// Computes the Airy function Ai(x).
    /// </summary>
    public static double AiryAi(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (double.IsNegativeInfinity(value)) {
            return 0;
        }

        if (Math.Abs(value) < AirySeriesBoundary) {
            var first = AirySeriesFirst(value);
            var second = AirySeriesSecond(value);
            return (AiryFirstZero * first) -
                (AiryFirstDerivativeZero * second);
        }

        if (value > 0) {
            var phase = (2.0 / 3.0) * Math.Pow(value, 1.5);
            var scale = 0.5 * Math.Exp(-phase) /
                Math.Sqrt(Constants.Pi * Math.Sqrt(value));
            return scale * AiryAsymptoticFirst(phase, value);
        } else {
            var magnitude = -value;
            var phase = (2.0 / 3.0) * Math.Pow(magnitude, 1.5);
            var angle = phase - Constants.PiOver4;
            var scale = 1 / Math.Sqrt(Constants.Pi * Math.Sqrt(magnitude));
            return scale *
                ((Math.Cos(angle) * AiryAsymptoticFirst(phase, phase)) -
                    (Math.Sin(angle) * AiryAsymptoticSecond(phase, phase)));
        }
    }

    /// <summary>
    /// Computes the Airy function Bi(x).
    /// </summary>
    public static double AiryBi(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return double.PositiveInfinity;
        }

        if (double.IsNegativeInfinity(value)) {
            return 0;
        }

        if (Math.Abs(value) < AirySeriesBoundary) {
            var first = AirySeriesFirst(value);
            var second = AirySeriesSecond(value);
            return Constants.Sqrt3 *
                ((AiryFirstZero * first) + (AiryFirstDerivativeZero * second));
        }

        if (value > 0) {
            var phase = (2.0 / 3.0) * Math.Pow(value, 1.5);
            var scale = Math.Exp(phase) /
                Math.Sqrt(Constants.Pi * Math.Sqrt(value));
            return scale * AiryAsymptoticFirst(phase, value);
        } else {
            var magnitude = -value;
            var phase = (2.0 / 3.0) * Math.Pow(magnitude, 1.5);
            var angle = phase - Constants.PiOver4;
            var scale = 1 / Math.Sqrt(Constants.Pi * Math.Sqrt(magnitude));
            return scale *
                ((-Math.Sin(angle) * AiryAsymptoticFirst(phase, phase)) -
                    (Math.Cos(angle) * AiryAsymptoticSecond(phase, phase)));
        }
    }

    private static double AirySeriesFirst(double value) {
        var sum = 1.0;
        var term = 1.0;
        for (var index = 0; index < MaximumIterations; index++) {
            term *= value * value * value /
                ((3.0 * index + 2) * (3.0 * index + 3));
            sum += term;
            if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                break;
            }
        }

        return sum;
    }

    private static double AirySeriesSecond(double value) {
        var sum = 1.0;
        var term = 1.0;
        for (var index = 0; index < MaximumIterations; index++) {
            term *= value * value * value /
                ((3.0 * index + 3) * (3.0 * index + 4));
            sum += term;
            if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                break;
            }
        }

        return value * sum;
    }

    private static double AiryAsymptoticFirst(double phase, double scale) {
        _ = phase;
        var inverse = 1 / scale;
        var square = inverse * inverse;
        return 1 - (square * (5.0 / 72.0)) + (square * square * (385.0 / 10368.0));
    }

    private static double AiryAsymptoticSecond(double phase, double scale) {
        _ = phase;
        var inverse = 1 / scale;
        var square = inverse * inverse;
        return (5.0 / 72.0) * inverse *
            (1 - (square * (385.0 / 10368.0)));
    }
}
