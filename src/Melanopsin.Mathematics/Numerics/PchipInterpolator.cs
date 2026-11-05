namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable piecewise cubic Hermite interpolant with shape-preserving PCHIP
/// slopes.
/// </summary>
/// <remarks>
/// This is the Fritsch-Carlson monotone cubic method (1980),
/// <see href="https://doi.org/10.1137/0717021">doi:10.1137/0717021</see>.
/// It preserves monotonicity of monotone data and avoids overshoot at local
/// extrema. Knots must be finite and strictly increasing. Evaluation is defined
/// only on the closed knot interval and throws outside it.
/// </remarks>
public sealed class PchipInterpolator {
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _derivatives;

    public PchipInterpolator(ReadOnlySpan<double> x, ReadOnlySpan<double> y) {
        if (x.Length < 2) {
            throw new ArgumentException(
                "At least two knots are required.",
                nameof(x));
        }

        if (y.Length < x.Length) {
            throw new ArgumentException(
                "The value sample is shorter than the abscissa sample.",
                nameof(y));
        }

        _x = x.ToArray();
        _y = y[..x.Length].ToArray();
        _derivatives = new double[x.Length];
        for (var index = 0; index < _x.Length; index++) {
            if (!double.IsFinite(_x[index]) || !double.IsFinite(_y[index])) {
                throw new ArgumentException(
                    "Knots and values must be finite.",
                    nameof(x));
            }

            if (index > 0 && _x[index] <= _x[index - 1]) {
                throw new ArgumentException(
                    "Knots must be strictly increasing.",
                    nameof(x));
            }
        }

        ComputeDerivatives();
    }

    public int Count => _x.Length;

    public double LeftEndpoint => _x[0];

    public double RightEndpoint => _x[^1];

    public ReadOnlySpan<double> Derivatives => _derivatives;

    /// <summary>
    /// Evaluates the interpolant at a point in the knot interval.
    /// </summary>
    public double Evaluate(double value) {
        var interval = FindInterval(value);
        var step = _x[interval + 1] - _x[interval];
        var parameter = (value - _x[interval]) / step;
        var parameterSquared = parameter * parameter;
        var parameterCubed = parameterSquared * parameter;
        var firstBasis = (2 * parameterCubed) - (3 * parameterSquared) + 1;
        var secondBasis = parameterCubed - (2 * parameterSquared) + parameter;
        var thirdBasis = -2 * parameterCubed + (3 * parameterSquared);
        var fourthBasis = parameterCubed - parameterSquared;
        return (firstBasis * _y[interval]) +
            (secondBasis * step * _derivatives[interval]) +
            (thirdBasis * _y[interval + 1]) +
            (fourthBasis * step * _derivatives[interval + 1]);
    }

    /// <summary>
    /// Evaluates the first derivative at a point in the knot interval.
    /// </summary>
    public double Derivative(double value) {
        var interval = FindInterval(value);
        var step = _x[interval + 1] - _x[interval];
        var parameter = (value - _x[interval]) / step;
        var parameterSquared = parameter * parameter;
        var firstBasis = (6 * parameterSquared) - (6 * parameter);
        var secondBasis = (3 * parameterSquared) - (4 * parameter) + 1;
        var thirdBasis = (-6 * parameterSquared) + (6 * parameter);
        var fourthBasis = (3 * parameterSquared) - (2 * parameter);
        return (firstBasis * _y[interval] / step) +
            (secondBasis * _derivatives[interval]) +
            (thirdBasis * _y[interval + 1] / step) +
            (fourthBasis * _derivatives[interval + 1]);
    }

    private void ComputeDerivatives() {
        var intervalCount = _x.Length - 1;
        if (intervalCount == 1) {
            _derivatives[0] = (_y[1] - _y[0]) / (_x[1] - _x[0]);
            _derivatives[1] = _derivatives[0];
            return;
        }

        for (var interval = 1; interval < intervalCount; interval++) {
            var previousStep = _x[interval] - _x[interval - 1];
            var nextStep = _x[interval + 1] - _x[interval];
            var previousSlope = (_y[interval] - _y[interval - 1]) / previousStep;
            var nextSlope = (_y[interval + 1] - _y[interval]) / nextStep;
            if (previousSlope == 0 ||
                nextSlope == 0 ||
                Math.Sign(previousSlope) != Math.Sign(nextSlope)) {
                _derivatives[interval] = 0;
                continue;
            }

            var previousWeight = (2 * nextStep) + previousStep;
            var nextWeight = nextStep + (2 * previousStep);
            _derivatives[interval] = (previousWeight + nextWeight) /
                ((previousWeight / previousSlope) + (nextWeight / nextSlope));
        }

        var firstStep = _x[1] - _x[0];
        var secondStep = _x[2] - _x[1];
        var firstSlope = (_y[1] - _y[0]) / firstStep;
        var secondSlope = (_y[2] - _y[1]) / secondStep;
        _derivatives[0] = EndpointSlope(
            firstStep,
            secondStep,
            firstSlope,
            secondSlope);
        var last = _x.Length - 1;
        var previousStepAtEnd = _x[last] - _x[last - 1];
        var finalStep = _x[last - 1] - _x[last - 2];
        var previousSlopeAtEnd = (_y[last] - _y[last - 1]) / previousStepAtEnd;
        var finalSlope = (_y[last - 1] - _y[last - 2]) / finalStep;
        _derivatives[last] = EndpointSlope(
            previousStepAtEnd,
            finalStep,
            previousSlopeAtEnd,
            finalSlope);
    }

    private static double EndpointSlope(
        double firstStep,
        double secondStep,
        double firstSlope,
        double secondSlope) {
        if (firstSlope == 0 ||
            Math.Sign(firstSlope) != Math.Sign(secondSlope)) {
            return 0;
        }

        var derivative =
            (((2 * firstStep) + secondStep) * firstSlope -
                (firstStep * secondSlope)) /
            (firstStep + secondStep);
        if (Math.Sign(derivative) != Math.Sign(firstSlope)) {
            return 0;
        }

        return Math.Abs(derivative) > 3 * Math.Abs(firstSlope)
            ? 3 * firstSlope
            : derivative;
    }

    private int FindInterval(double value) {
        if (!double.IsFinite(value) ||
            value < _x[0] ||
            value > _x[^1]) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The query point must be finite and inside the knot interval.");
        }

        if (value == _x[^1]) {
            return _x.Length - 2;
        }

        var lower = 0;
        var upper = _x.Length - 1;
        while (upper - lower > 1) {
            var middle = lower + ((upper - lower) / 2);
            if (value < _x[middle]) {
                upper = middle;
            } else {
                lower = middle;
            }
        }

        return lower;
    }
}
