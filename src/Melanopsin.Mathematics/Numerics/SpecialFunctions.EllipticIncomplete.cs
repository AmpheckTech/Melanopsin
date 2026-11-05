namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private const double CarlsonTolerance = 1e-12;
    private const int CarlsonMaximumIterations = 100;

    /// <summary>
    /// Computes the incomplete elliptic integral of the first kind,
    /// <c>F(phi | m) = integral(0, phi, (1 - m sin^2(theta))^-1/2 dtheta)</c>.
    /// </summary>
    /// <param name="amplitude">
    /// The angular upper limit in radians. The principal interval
    /// <c>-pi/2 &lt;= phi &lt;= pi/2</c> is supported.
    /// </param>
    /// <param name="parameter">
    /// The elliptic parameter <c>m = k^2</c>, not the modulus <c>k</c>.
    /// Values at most one are supported; <c>m = 1</c> is the logarithmic
    /// endpoint case.
    /// </param>
    /// <remarks>
    /// This method does not silently reduce amplitudes outside the principal
    /// interval because the complete-integral period terms depend on the
    /// caller's branch conventions. Such arguments return NaN.
    /// </remarks>
    public static double IncompleteEllipticIntegralFirstKind(
        double amplitude,
        double parameter) {
        if (!TryValidateIncompleteEllipticArguments(
            amplitude,
            parameter,
            out var sine,
            out var cosineSquared,
            out var complementaryParameter)) {
            return double.NaN;
        }

        if (amplitude == 0) {
            return 0;
        }

        if (Math.Abs(amplitude) == Constants.PiOver2) {
            return CompleteEllipticIntegralFirstKind(parameter);
        }

        if (parameter == 1) {
            return Math.Atanh(sine);
        }

        return sine * CarlsonRf(cosineSquared, complementaryParameter);
    }

    /// <summary>
    /// Computes the incomplete elliptic integral of the second kind,
    /// <c>E(phi | m) = integral(0, phi, sqrt(1 - m sin^2(theta)) dtheta)</c>.
    /// </summary>
    /// <param name="amplitude">
    /// The angular upper limit in radians in the principal interval
    /// <c>-pi/2 &lt;= phi &lt;= pi/2</c>.
    /// </param>
    /// <param name="parameter">
    /// The elliptic parameter <c>m = k^2</c>, not the modulus <c>k</c>.
    /// Values at most one are supported.
    /// </param>
    public static double IncompleteEllipticIntegralSecondKind(
        double amplitude,
        double parameter) {
        if (!TryValidateIncompleteEllipticArguments(
            amplitude,
            parameter,
            out var sine,
            out var cosineSquared,
            out var complementaryParameter)) {
            return double.NaN;
        }

        if (amplitude == 0) {
            return 0;
        }

        if (Math.Abs(amplitude) == Constants.PiOver2) {
            return CompleteEllipticIntegralSecondKind(parameter);
        }

        if (parameter == 1) {
            return sine;
        }

        var sineSquared = sine * sine;
        var firstKind = sine * CarlsonRf(cosineSquared, complementaryParameter);
        var thirdSymmetric = CarlsonRd(
            cosineSquared,
            complementaryParameter,
            1);
        return firstKind - ((parameter / 3) * sineSquared * sine * thirdSymmetric);
    }

    private static bool TryValidateIncompleteEllipticArguments(
        double amplitude,
        double parameter,
        out double sine,
        out double cosineSquared,
        out double complementaryParameter) {
        sine = 0;
        cosineSquared = 0;
        complementaryParameter = 0;
        if (!double.IsFinite(amplitude) || !double.IsFinite(parameter) ||
            Math.Abs(amplitude) > Constants.PiOver2 || parameter > 1) {
            return false;
        }

        sine = Math.Sin(amplitude);
        cosineSquared = Math.Max(0, 1 - (sine * sine));
        complementaryParameter = 1 - (parameter * sine * sine);
        return complementaryParameter >= 0;
    }

    private static double CarlsonRf(double x, double y) {
        if ((x == 0 && y == 0) || !double.IsFinite(x) || !double.IsFinite(y) ||
            x < 0 || y < 0) {
            return double.NaN;
        }

        var currentX = x;
        var currentY = y;
        var currentZ = 1.0;
        for (var iteration = 0; iteration < CarlsonMaximumIterations; iteration++) {
            var squareRootX = Math.Sqrt(currentX);
            var squareRootY = Math.Sqrt(currentY);
            var squareRootZ = Math.Sqrt(currentZ);
            var lambda = (squareRootX * (squareRootY + squareRootZ)) +
                (squareRootY * squareRootZ);
            currentX = 0.25 * (currentX + lambda);
            currentY = 0.25 * (currentY + lambda);
            currentZ = 0.25 * (currentZ + lambda);
            var average = (currentX + currentY + currentZ) / 3;
            var deltaX = (average - currentX) / average;
            var deltaY = (average - currentY) / average;
            var deltaZ = (average - currentZ) / average;
            if (Math.Max(
                Math.Abs(deltaX),
                Math.Max(Math.Abs(deltaY), Math.Abs(deltaZ))) <= CarlsonTolerance) {
                var squared = (deltaX * deltaY) - (deltaZ * deltaZ);
                var cubed = deltaX * deltaY * deltaZ;
                var correction = 1 +
                    (((squared / 24) - 0.1 - ((3 * cubed) / 44)) * squared) +
                    (cubed / 14);
                return correction / Math.Sqrt(average);
            }
        }

        return double.NaN;
    }

    private static double CarlsonRd(double x, double y, double z) {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) ||
            x < 0 || y < 0 || z <= 0 || (x == 0 && y == 0)) {
            return double.NaN;
        }

        const double c1 = 3.0 / 14.0;
        const double c2 = 1.0 / 6.0;
        const double c3 = 9.0 / 22.0;
        const double c4 = 3.0 / 26.0;
        const double c5 = 0.25 * c3;
        const double c6 = 1.5 * c4;
        var currentX = x;
        var currentY = y;
        var currentZ = z;
        var sum = 0.0;
        var factor = 1.0;
        for (var iteration = 0; iteration < CarlsonMaximumIterations; iteration++) {
            var squareRootX = Math.Sqrt(currentX);
            var squareRootY = Math.Sqrt(currentY);
            var squareRootZ = Math.Sqrt(currentZ);
            var lambda = (squareRootX * (squareRootY + squareRootZ)) +
                (squareRootY * squareRootZ);
            sum += factor / (squareRootZ * (currentZ + lambda));
            factor *= 0.25;
            currentX = 0.25 * (currentX + lambda);
            currentY = 0.25 * (currentY + lambda);
            currentZ = 0.25 * (currentZ + lambda);
            var average = (currentX + currentY + (3 * currentZ)) / 5;
            var deltaX = (average - currentX) / average;
            var deltaY = (average - currentY) / average;
            var deltaZ = (average - currentZ) / average;
            if (Math.Max(
                Math.Abs(deltaX),
                Math.Max(Math.Abs(deltaY), Math.Abs(deltaZ))) <= CarlsonTolerance) {
                var product = deltaX * deltaY;
                var squaredZ = deltaZ * deltaZ;
                var difference = product - squaredZ;
                var combined = difference - (6 * squaredZ);
                var combinedTwice = combined + difference + difference;
                var correction = 1 +
                    (combined *
                        (-c1 + (c5 * combined) - (c6 * deltaZ * combinedTwice))) +
                    (deltaZ *
                        ((c2 * combinedTwice) +
                            (deltaZ *
                                (-(c3 * difference) + (deltaZ * c4 * product)))));
                return (3 * sum) +
                    (factor * correction / (average * Math.Sqrt(average)));
            }
        }

        return double.NaN;
    }
}
