namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable piecewise cubic Hermite interpolant with caller-supplied
/// derivatives.
/// </summary>
/// <remarks>
/// Knots must be finite and strictly increasing. Evaluation is defined only on
/// the closed knot interval; querying outside it throws rather than silently
/// extrapolating. The interpolant is <c>C1</c> because adjacent segments share
/// both endpoint values and derivatives.
/// </remarks>
public sealed class CubicHermiteInterpolator {
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _derivatives;

    public CubicHermiteInterpolator(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        ReadOnlySpan<double> derivatives) {
        if (x.IsEmpty) {
            throw new ArgumentException(
                "At least one knot is required.",
                nameof(x));
        }

        if (y.Length < x.Length) {
            throw new ArgumentException(
                "The value sample is shorter than the abscissa sample.",
                nameof(y));
        }

        if (derivatives.Length < x.Length) {
            throw new ArgumentException(
                "The derivative sample is shorter than the abscissa sample.",
                nameof(derivatives));
        }

        _x = x.ToArray();
        _y = y[..x.Length].ToArray();
        _derivatives = derivatives[..x.Length].ToArray();
        for (var index = 0; index < _x.Length; index++) {
            if (!double.IsFinite(_x[index]) ||
                !double.IsFinite(_y[index]) ||
                !double.IsFinite(_derivatives[index])) {
                throw new ArgumentException(
                    "Knots, values, and derivatives must be finite.",
                    nameof(x));
            }

            if (index > 0 && _x[index] <= _x[index - 1]) {
                throw new ArgumentException(
                    "Knots must be strictly increasing.",
                    nameof(x));
            }
        }
    }

    public int Count => _x.Length;

    public double LeftEndpoint => _x[0];

    public double RightEndpoint => _x[^1];

    /// <summary>
    /// Evaluates the Hermite interpolant at a point in the knot interval.
    /// </summary>
    public double Evaluate(double value) {
        if (_x.Length == 1) {
            ValidateQuery(value);
            return _y[0];
        }

        var interval = FindInterval(value);
        var left = _x[interval];
        var step = _x[interval + 1] - left;
        var parameter = (value - left) / step;
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
    /// Evaluates the first derivative of the Hermite interpolant.
    /// </summary>
    public double Derivative(double value) {
        if (_x.Length == 1) {
            ValidateQuery(value);
            return _derivatives[0];
        }

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

    private int FindInterval(double value) {
        ValidateQuery(value);
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

    private void ValidateQuery(double value) {
        if (!double.IsFinite(value) ||
            value < _x[0] ||
            value > _x[^1]) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The query point must be finite and inside the knot interval.");
        }
    }
}
