namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the logarithm of the multivariate gamma function
    /// <c>Gamma_p(a)</c> for a positive shape and positive dimension.
    /// </summary>
    /// <remarks>
    /// The implementation uses
    /// <c>pi^(p (p - 1) / 4) prod(j=0..p-1) Gamma(a - j/2)</c> in log space.
    /// </remarks>
    public static double LogMultivariateGamma(double shape, int dimension) {
        if (!(shape > 0) || double.IsNaN(shape)) {
            return double.NaN;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimension);
        var result = 0.25 * dimension * (dimension - 1) * Math.Log(Constants.Pi);
        for (var index = 0; index < dimension; index++) {
            result += LogGamma(shape - (0.5 * index));
        }

        return result;
    }
}
