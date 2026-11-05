// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the logistic sigmoid <c>1 / (1 + exp(-value))</c> without
    /// overflowing for large finite magnitudes.
    /// </summary>
    public static double Sigmoid(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value)) {
            return 1;
        }

        if (double.IsNegativeInfinity(value)) {
            return 0;
        }

        if (value >= 0) {
            return 1 / (1 + Math.Exp(-value));
        }

        var exponential = Math.Exp(value);
        return exponential / (1 + exponential);
    }

    /// <summary>
    /// Computes the inverse logistic function <c>log(p / (1 - p))</c>.
    /// </summary>
    /// <remarks>
    /// The endpoints zero and one map to negative and positive infinity,
    /// respectively. Values outside the closed unit interval and NaN return
    /// NaN.
    /// </remarks>
    public static double Logit(double probability) {
        if (double.IsNaN(probability) ||
            probability < 0 ||
            probability > 1) {
            return double.NaN;
        }

        if (probability == 0) {
            return double.NegativeInfinity;
        }

        if (probability == 1) {
            return double.PositiveInfinity;
        }

        return Math.Log(probability) - FloatingPointMath.Log1p(-probability);
    }

    /// <summary>
    /// Computes a numerically stable softmax over a finite or infinite
    /// real-valued input.
    /// </summary>
    /// <remarks>
    /// If any input is NaN, every destination value is NaN. If one or more
    /// inputs are positive infinity, the probability mass is split equally
    /// among those entries. If every input is negative infinity, the softmax
    /// is mathematically undefined and every destination value is NaN. The
    /// destination may overlap the input.
    /// </remarks>
    public static void Softmax(
        ReadOnlySpan<double> values,
        Span<double> destination) {
        ValidateSoftmaxBuffers(values, destination);
        var maximum = double.NegativeInfinity;
        var positiveInfinityCount = 0;
        var hasNaN = false;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            if (double.IsNaN(value)) {
                hasNaN = true;
            } else if (double.IsPositiveInfinity(value)) {
                positiveInfinityCount++;
            } else if (value > maximum) {
                maximum = value;
            }
        }

        if (hasNaN || (positiveInfinityCount == 0 && maximum == double.NegativeInfinity)) {
            destination.Fill(double.NaN);
            return;
        }

        if (positiveInfinityCount > 0) {
            var share = 1.0 / positiveInfinityCount;
            for (var index = 0; index < values.Length; index++) {
                destination[index] = double.IsPositiveInfinity(values[index])
                    ? share
                    : 0;
            }

            return;
        }

        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var value = Math.Exp(values[index] - maximum);
            destination[index] = value;
            sum += value;
        }

        for (var index = 0; index < destination.Length; index++) {
            destination[index] /= sum;
        }
    }

    /// <summary>
    /// Computes a numerically stable log-softmax over a finite or infinite
    /// real-valued input.
    /// </summary>
    /// <remarks>
    /// If any input is NaN, every destination value is NaN. If one or more
    /// inputs are positive infinity, those entries receive
    /// <c>-log(count)</c> and all other entries receive negative infinity. If
    /// every input is negative infinity, every destination value is NaN. The
    /// destination may overlap the input.
    /// </remarks>
    public static void LogSoftmax(
        ReadOnlySpan<double> values,
        Span<double> destination) {
        ValidateSoftmaxBuffers(values, destination);
        var maximum = double.NegativeInfinity;
        var positiveInfinityCount = 0;
        var hasNaN = false;
        for (var index = 0; index < values.Length; index++) {
            var value = values[index];
            if (double.IsNaN(value)) {
                hasNaN = true;
            } else if (double.IsPositiveInfinity(value)) {
                positiveInfinityCount++;
            } else if (value > maximum) {
                maximum = value;
            }
        }

        if (hasNaN || (positiveInfinityCount == 0 && maximum == double.NegativeInfinity)) {
            destination.Fill(double.NaN);
            return;
        }

        if (positiveInfinityCount > 0) {
            var share = -Math.Log(positiveInfinityCount);
            for (var index = 0; index < values.Length; index++) {
                destination[index] = double.IsPositiveInfinity(values[index])
                    ? share
                    : double.NegativeInfinity;
            }

            return;
        }

        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            sum += Math.Exp(values[index] - maximum);
        }

        var logarithm = maximum + Math.Log(sum);
        for (var index = 0; index < destination.Length; index++) {
            destination[index] = values[index] - logarithm;
        }
    }

    private static void ValidateSoftmaxBuffers(
        ReadOnlySpan<double> values,
        Span<double> destination) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        if (destination.Length < values.Length) {
            throw new ArgumentException(
                "The destination must contain at least one entry per input.",
                nameof(destination));
        }
    }
}
