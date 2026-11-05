namespace Melanopsin.Mathematics;

/// <summary>
/// Allocation-free interpolation helpers for one-dimensional samples.
/// </summary>
public static class Interpolation {
    /// <summary>
    /// Evaluates linear interpolation, without clamping the parameter.
    /// </summary>
    public static double Linear(
        double x0,
        double y0,
        double x1,
        double y1,
        double x) {
        if (x0 == x1) {
            throw new ArgumentException(
                "The sample abscissae must be distinct.",
                nameof(x1));
        }

        return y0 + ((x - x0) * ((y1 - y0) / (x1 - x0)));
    }

    /// <summary>
    /// Evaluates the unique polynomial through the supplied distinct nodes
    /// using Lagrange interpolation.
    /// </summary>
    public static double Lagrange(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double value) {
        ValidateSamples(x, y);
        var result = 0.0;
        for (var i = 0; i < x.Length; i++) {
            var basis = 1.0;
            for (var j = 0; j < x.Length; j++) {
                if (i == j) {
                    continue;
                }

                basis *= (value - x[j]) / (x[i] - x[j]);
            }

            result += y[i] * basis;
        }

        return result;
    }

    /// <summary>
    /// Evaluates polynomial interpolation using Neville's algorithm. This is
    /// better conditioned than expanding the interpolating polynomial.
    /// </summary>
    public static double Neville(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double value,
        Span<double> workspace) {
        ValidateSamples(x, y);
        if (workspace.Length < x.Length) {
            throw new ArgumentException(
                "The workspace must contain at least one value per node.",
                nameof(workspace));
        }

        y.CopyTo(workspace);
        for (var level = 1; level < x.Length; level++) {
            for (var index = x.Length - 1; index >= level; index--) {
                workspace[index] =
                    (((value - x[index - level]) * workspace[index]) -
                    ((value - x[index]) * workspace[index - 1])) /
                    (x[index] - x[index - level]);
            }
        }

        return workspace[x.Length - 1];
    }

    /// <summary>
    /// Computes the barycentric weights for the supplied distinct nodes.
    /// </summary>
    public static bool TryComputeBarycentricWeights(
        ReadOnlySpan<double> x,
        Span<double> weights) {
        return TryComputeBarycentricWeights(x, weights, out _);
    }

    /// <summary>
    /// Computes barycentric weights and reports non-finite nodes or weights.
    /// </summary>
    /// <remarks>
    /// Duplicate nodes are a programming error because they define no valid
    /// interpolation problem and therefore throw an <see cref="ArgumentException"/>.
    /// </remarks>
    public static bool TryComputeBarycentricWeights(
        ReadOnlySpan<double> x,
        Span<double> weights,
        out InterpolationReport report) {
        if (weights.Length < x.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(weights));
        }

        for (var i = 0; i < x.Length; i++) {
            if (!double.IsFinite(x[i])) {
                weights[..x.Length].Clear();
                report = new InterpolationReport(
                    InterpolationOperation.BarycentricWeights,
                    InterpolationStatus.NonFiniteInput);
                return false;
            }

            for (var j = 0; j < i; j++) {
                if (x[i] == x[j]) {
                    throw new ArgumentException(
                        "Interpolation nodes must be distinct.",
                        nameof(x));
                }
            }
        }

        for (var i = 0; i < x.Length; i++) {
            var product = 1.0;
            for (var j = 0; j < x.Length; j++) {
                if (i != j) {
                    product *= x[i] - x[j];
                }
            }

            weights[i] = 1 / product;
            if (!double.IsFinite(weights[i])) {
                weights[..x.Length].Clear();
                report = new InterpolationReport(
                    InterpolationOperation.BarycentricWeights,
                    InterpolationStatus.NonFiniteOutput);
                return false;
            }
        }

        report = new InterpolationReport(
            InterpolationOperation.BarycentricWeights,
            InterpolationStatus.Succeeded);
        return true;
    }

    private static void ValidateSamples(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y) {
        if (x.Length == 0) {
            throw new ArgumentException(
                "At least one sample is required.",
                nameof(x));
        }

        if (y.Length < x.Length) {
            throw new ArgumentException(
                "The value sample is shorter than the abscissa sample.",
                nameof(y));
        }

        var elementCount = x.Length;
        for (var i = 0; i < elementCount; i++) {
            if (!double.IsFinite(x[i]) || !double.IsFinite(y[i])) {
                throw new ArgumentException(
                    "Interpolation samples must be finite.",
                    nameof(y));
            }

            for (var j = 0; j < i; j++) {
                if (x[i] == x[j]) {
                    throw new ArgumentException(
                        "Interpolation nodes must be distinct.",
                        nameof(x));
                }
            }
        }
    }
}
