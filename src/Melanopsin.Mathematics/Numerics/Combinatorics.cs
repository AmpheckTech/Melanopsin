// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Exact-count helpers expressed in double precision and common combinatorial
/// functions.
/// </summary>
/// <remarks>
/// Results that exceed <see cref="double.MaxValue"/> are returned as positive
/// infinity. Integer counts beyond the exactly representable range therefore
/// remain useful as floating-point magnitudes; use the logarithmic variants for
/// stable comparisons and ratios.
/// </remarks>
public static class Combinatorics {
    public static double Factorial(int value) {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        var result = 1.0;
        for (var current = 2; current <= value; current++) {
            result *= current;
        }

        return result;
    }

    public static double LogFactorial(int value) {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        return value < 2 ? 0 : SpecialFunctions.LogGamma(value + 1.0);
    }

    public static double DoubleFactorial(int value) {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);

        var result = 1.0;
        for (var current = value; current > 1; current -= 2) {
            result *= current;
        }

        return result;
    }

    /// <summary>
    /// Computes <c>n! / (k! (n-k)!)</c>. Out-of-range selections return zero.
    /// </summary>
    public static double Binomial(int count, int selection) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (selection < 0 || selection > count) {
            return 0;
        }

        selection = Math.Min(selection, count - selection);
        var result = 1.0;
        for (var index = 1; index <= selection; index++) {
            result *= (count - selection) + index;
            result /= index;
        }

        return result;
    }

    public static double LogBinomial(int count, int selection) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (selection < 0 || selection > count) {
            return double.NegativeInfinity;
        }

        return LogFactorial(count) -
            LogFactorial(selection) -
            LogFactorial(count - selection);
    }

    /// <summary>
    /// Computes the falling factorial <c>x (x-1) ... (x-k+1)</c>.
    /// </summary>
    public static double FallingFactorial(double value, int count) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = 1.0;
        for (var index = 0; index < count; index++) {
            result *= value - index;
        }

        return result;
    }

    /// <summary>
    /// Computes the rising factorial <c>x (x+1) ... (x+k-1)</c>.
    /// </summary>
    public static double RisingFactorial(double value, int count) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = 1.0;
        for (var index = 0; index < count; index++) {
            result *= value + index;
        }

        return result;
    }

    public static double Permutations(int count, int selection) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (selection < 0 || selection > count) {
            return 0;
        }

        return FallingFactorial(count, selection);
    }

    public static double Multinomial(ReadOnlySpan<int> counts) {
        var total = 0;
        for (var index = 0; index < counts.Length; index++) {
            ArgumentOutOfRangeException.ThrowIfNegative(counts[index], nameof(counts));
            total = checked(total + counts[index]);
        }

        var result = 1.0;
        var remaining = total;
        for (var index = 0; index < counts.Length; index++) {
            result *= Binomial(remaining, counts[index]);
            remaining -= counts[index];
        }

        return result;
    }

    /// <summary>
    /// Computes the logarithm of a multinomial coefficient.
    /// </summary>
    public static double LogMultinomial(ReadOnlySpan<int> counts) {
        var total = 0;
        var denominator = 0.0;
        for (var index = 0; index < counts.Length; index++) {
            ArgumentOutOfRangeException.ThrowIfNegative(counts[index], nameof(counts));
            total = checked(total + counts[index]);
            denominator += LogFactorial(counts[index]);
        }

        return LogFactorial(total) - denominator;
    }
}
