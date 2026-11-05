// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An immutable barycentric polynomial interpolator for repeated evaluation.
/// </summary>
/// <remarks>
/// Evaluation uses the second barycentric form discussed by Berrut and
/// Trefethen (2004),
/// <see href="https://doi.org/10.1137/S0036144502417715">doi:10.1137/S0036144502417715</see>.
/// </remarks>
public sealed class BarycentricInterpolator {
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _weights;

    public BarycentricInterpolator(
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

        _x = x[..(x.Length)].ToArray();
        _y = y[..x.Length].ToArray();
        _weights = new double[x.Length];
        if (!Interpolation.TryComputeBarycentricWeights(_x, _weights)) {
            throw new ArgumentException(
                "The interpolation nodes must be finite and distinct.",
                nameof(x));
        }

        for (var index = 0; index < _y.Length; index++) {
            if (!double.IsFinite(_y[index])) {
                throw new ArgumentException(
                    "Interpolation values must be finite.",
                    nameof(y));
            }
        }
    }

    public int Count => _x.Length;

    public ReadOnlySpan<double> Nodes => _x;

    public ReadOnlySpan<double> Values => _y;

    public ReadOnlySpan<double> Weights => _weights;

    public double Evaluate(double value) {
        for (var index = 0; index < _x.Length; index++) {
            if (value == _x[index]) {
                return _y[index];
            }
        }

        var numerator = 0.0;
        var denominator = 0.0;
        for (var index = 0; index < _x.Length; index++) {
            var term = _weights[index] / (value - _x[index]);
            numerator += term * _y[index];
            denominator += term;
        }

        return numerator / denominator;
    }
}
