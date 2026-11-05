namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private const double ModifiedBesselSeriesBoundary = 20;
    private const double ModifiedBesselKSeriesBoundary = 10;
    private const int ModifiedBesselAsymptoticTermCount = 14;
    private const double SqrtTwoPi = 2.5066282746310005024157652848110452530069867406099;

    /// <summary>
    /// Computes the integer-order modified Bessel function of the first kind,
    /// I_n(x).
    /// </summary>
    public static double BesselI(int order, double value) {
        if (order < 0) {
            ArgumentOutOfRangeException.ThrowIfEqual(order, int.MinValue);
            order = -order;
        }

        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return double.PositiveInfinity;
        }

        var absoluteValue = Math.Abs(value);
        double result;
        if (order == 0) {
            result = BesselI0(absoluteValue);
        } else if (order == 1) {
            result = BesselI1(absoluteValue);
        } else if (absoluteValue < ModifiedBesselSeriesBoundary ||
            order > absoluteValue) {
            result = BesselISeries(order, absoluteValue);
        } else {
            var previous = BesselI0(absoluteValue);
            var current = BesselI1(absoluteValue);
            for (var index = 1; index < order; index++) {
                var next = previous - ((2.0 * index / absoluteValue) * current);
                previous = current;
                current = next;
                if (!double.IsFinite(current)) {
                    break;
                }
            }

            result = current;
        }

        return value < 0 && (order & 1) != 0 ? -result : result;
    }

    /// <summary>Computes I_0(x).</summary>
    public static double BesselI0(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return double.PositiveInfinity;
        }

        var absoluteValue = Math.Abs(value);
        if (absoluteValue < ModifiedBesselSeriesBoundary) {
            var scaledSquare = 0.25 * absoluteValue * absoluteValue;
            var term = 1.0;
            var sum = 1.0;
            for (var index = 1; index < MaximumIterations; index++) {
                term *= scaledSquare / (index * index);
                sum += term;
                if (term <= sum * RelativeAccuracy) {
                    return sum;
                }
            }

            return sum;
        }

        if (absoluteValue > 710) {
            return double.PositiveInfinity;
        }

        return Math.Exp(absoluteValue) /
            (SqrtTwoPi * Math.Sqrt(absoluteValue)) *
            ModifiedBesselAsymptotic(absoluteValue, 0);
    }

    /// <summary>Computes I_1(x).</summary>
    public static double BesselI1(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return value > 0
                ? double.PositiveInfinity
                : double.NegativeInfinity;
        }

        var absoluteValue = Math.Abs(value);
        double result;
        if (absoluteValue < ModifiedBesselSeriesBoundary) {
            var scaledSquare = 0.25 * absoluteValue * absoluteValue;
            var term = 0.5 * absoluteValue;
            var sum = term;
            for (var index = 1; index < MaximumIterations; index++) {
                term *= scaledSquare / (index * (index + 1));
                sum += term;
                if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                    break;
                }
            }

            result = sum;
        } else {
            result = absoluteValue > 710
                ? double.PositiveInfinity
                : Math.Exp(absoluteValue) /
                    (SqrtTwoPi * Math.Sqrt(absoluteValue)) *
                    ModifiedBesselAsymptotic(absoluteValue, 1);
        }

        return value < 0 ? -result : result;
    }

    /// <summary>
    /// Computes the integer-order modified Bessel function of the second kind,
    /// K_n(x), for positive real arguments.
    /// </summary>
    /// <remarks>
    /// K_n has a branch point at zero. Zero returns positive infinity and a
    /// negative argument returns NaN because the principal value is generally
    /// complex there.
    /// </remarks>
    public static double BesselK(int order, double value) {
        if (order < 0) {
            ArgumentOutOfRangeException.ThrowIfEqual(order, int.MinValue);
            order = -order;
        }

        if (double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return double.PositiveInfinity;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (order == 0) {
            return BesselK0(value);
        }

        if (order == 1) {
            return BesselK1(value);
        }

        var previous = BesselK0(value);
        var current = BesselK1(value);
        for (var index = 1; index < order; index++) {
            var next = previous + ((2.0 * index / value) * current);
            previous = current;
            current = next;
            if (!double.IsFinite(current)) {
                break;
            }
        }

        return current;
    }

    /// <summary>Computes K_0(x) for x greater than or equal to zero.</summary>
    public static double BesselK0(double value) {
        if (double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return double.PositiveInfinity;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value >= ModifiedBesselKSeriesBoundary) {
            return Math.Exp(-value) * Math.Sqrt(Constants.Pi / (2 * value)) *
                ModifiedBesselKAsymptotic(value, 0);
        }

        var i0 = BesselI0(value);
        var logarithm = Math.Log(0.5 * value) + Constants.EulerMascheroni;
        var scaledSquare = 0.25 * value * value;
        var term = 1.0;
        var harmonic = 0.0;
        var series = 0.0;
        for (var index = 1; index < MaximumIterations; index++) {
            term *= scaledSquare / (index * index);
            harmonic += 1.0 / index;
            var contribution = harmonic * term;
            series += contribution;
            if (contribution <= Math.Max(Math.Abs(series), 1) * RelativeAccuracy) {
                break;
            }
        }

        return -(logarithm * i0) + series;
    }

    /// <summary>Computes K_1(x) for x greater than zero.</summary>
    public static double BesselK1(double value) {
        if (double.IsNaN(value) || value < 0) {
            return double.NaN;
        }

        if (value == 0) {
            return double.PositiveInfinity;
        }

        if (double.IsPositiveInfinity(value)) {
            return 0;
        }

        if (value >= ModifiedBesselKSeriesBoundary) {
            return Math.Exp(-value) * Math.Sqrt(Constants.Pi / (2 * value)) *
                ModifiedBesselKAsymptotic(value, 1);
        }

        var i0 = BesselI0(value);
        var i1 = BesselI1(value);
        var logarithm = Math.Log(0.5 * value) + Constants.EulerMascheroni;
        var scaledSquare = 0.25 * value * value;
        var term = 1.0;
        var harmonic = 0.0;
        var series = 0.0;
        for (var index = 1; index < MaximumIterations; index++) {
            harmonic += 1.0 / index;
            var contribution = harmonic * term;
            series += contribution;
            if (contribution <= Math.Max(Math.Abs(series), 1) * RelativeAccuracy) {
                break;
            }

            term *= scaledSquare / (index * (index + 1));
        }

        return (i0 / value) + (logarithm * i1) - (0.5 * value * series);
    }

    private static double BesselISeries(int order, double value) {
        var half = 0.5 * value;
        var term = 1.0;
        for (var index = 1; index <= order; index++) {
            term *= half / index;
        }

        var sum = term;
        var scaledSquare = half * half;
        for (var index = 1; index < MaximumIterations; index++) {
            term *= scaledSquare / (index * (order + index));
            sum += term;
            if (term <= Math.Abs(sum) * RelativeAccuracy) {
                break;
            }
        }

        return sum;
    }

    private static double ModifiedBesselAsymptotic(double value, int order) {
        var mu = 4.0 * order * order;
        var inverse = 1 / (8 * value);
        var term = 1.0;
        var sum = 1.0;
        for (var index = 1; index <= ModifiedBesselAsymptoticTermCount; index++) {
            term *= -((mu - ((2 * index - 1) * (2 * index - 1))) * inverse / index);
            sum += term;
        }

        return sum;
    }

    private static double ModifiedBesselKAsymptotic(double value, int order) {
        var mu = 4.0 * order * order;
        var inverse = 1 / (8 * value);
        var term = 1.0;
        var sum = 1.0;
        for (var index = 1; index <= ModifiedBesselAsymptoticTermCount; index++) {
            term *= (mu - ((2 * index - 1) * (2 * index - 1))) * inverse / index;
            sum += term;
        }

        return sum;
    }
}
