// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Periodic and symmetric cosine-sum window functions.
/// </summary>
/// <remarks>
/// Symmetric windows include both endpoints and are suitable for filter design.
/// Periodic windows are intended for spectral analysis and satisfy
/// <c>w[n] = w[n + N]</c>. For a periodic window the denominator is
/// <c>N</c>; for a symmetric window it is <c>N - 1</c>. A one-sample window is
/// always one, and a two-sample symmetric window is two ones. Cosine-sum window
/// terminology and Blackman-Harris weighting follow Harris (1978),
/// <see href="https://doi.org/10.1109/PROC.1978.10837">doi:10.1109/PROC.1978.10837</see>.
/// </remarks>
public static partial class WindowFunctions {
    public static double[] Hann(int length, bool periodic = true) {
        return Create(length, periodic, static (index, count) =>
            0.5 - (0.5 * Math.Cos(Constants.Tau * index / count)));
    }

    public static double[] Hamming(int length, bool periodic = true) {
        return Create(length, periodic, static (index, count) =>
            0.54 - (0.46 * Math.Cos(Constants.Tau * index / count)));
    }

    public static double[] Blackman(int length, bool periodic = true) {
        return Create(length, periodic, static (index, count) =>
            (0.42 -
                (0.5 * Math.Cos(Constants.Tau * index / count))) +
                (0.08 * Math.Cos(2 * Constants.Tau * index / count)));
    }

    public static double[] BlackmanHarris(int length, bool periodic = true) {
        return Create(length, periodic, static (index, count) =>
            ((0.35875 -
                (0.48829 * Math.Cos(Constants.Tau * index / count))) +
                (0.14128 * Math.Cos(2 * Constants.Tau * index / count))) -
                (0.01168 * Math.Cos(3 * Constants.Tau * index / count)));
    }

    /// <summary>
    /// Fills a destination with a Hann window.
    /// </summary>
    public static void Hann(Span<double> destination, bool periodic = true) {
        Fill(destination, periodic, static (index, count) =>
            0.5 - (0.5 * Math.Cos(Constants.Tau * index / count)));
    }

    public static void Hamming(Span<double> destination, bool periodic = true) {
        Fill(destination, periodic, static (index, count) =>
            0.54 - (0.46 * Math.Cos(Constants.Tau * index / count)));
    }

    public static void Blackman(Span<double> destination, bool periodic = true) {
        Fill(destination, periodic, static (index, count) =>
            (0.42 -
                (0.5 * Math.Cos(Constants.Tau * index / count))) +
                (0.08 * Math.Cos(2 * Constants.Tau * index / count)));
    }

    public static void BlackmanHarris(
        Span<double> destination,
        bool periodic = true) {
        Fill(destination, periodic, static (index, count) =>
            ((0.35875 -
                (0.48829 * Math.Cos(Constants.Tau * index / count))) +
                (0.14128 * Math.Cos(2 * Constants.Tau * index / count))) -
                (0.01168 * Math.Cos(3 * Constants.Tau * index / count)));
    }

    private static double[] Create(
        int length,
        bool periodic,
        Func<int, int, double> evaluate) {
        var result = new double[length];
        Fill(result, periodic, evaluate);
        return result;
    }

    private static void Fill(
        Span<double> destination,
        bool periodic,
        Func<int, int, double> evaluate) {
        ArgumentNullException.ThrowIfNull(evaluate);
        if (destination.IsEmpty) {
            return;
        }

        var count = periodic ? destination.Length : destination.Length - 1;
        if (count <= 0) {
            destination.Fill(1);
            return;
        }

        for (var index = 0; index < destination.Length; index++) {
            destination[index] = evaluate(index, count);
        }
    }
}
