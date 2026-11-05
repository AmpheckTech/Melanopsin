// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class Fourier {
    /// <summary>
    /// Returns the signed DFT bin frequencies in hertz for a real sample rate.
    /// </summary>
    /// <remarks>
    /// For a length <c>N</c>, the result spans <c>0</c> through the positive
    /// Nyquist region and then negative frequencies. For odd lengths the
    /// negative region has one more bin than the positive non-DC region.
    /// </remarks>
    public static double[] Frequencies(int length, double sampleRate) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ValidateSampleRate(sampleRate);
        var result = new double[length];
        var binWidth = sampleRate / length;
        for (var index = 0; index < length; index++) {
            var bin = index <= length / 2 ? index : index - length;
            result[index] = bin * binWidth;
        }

        return result;
    }

    /// <summary>
    /// Returns the non-negative DFT bin frequencies in hertz, including the
    /// Nyquist bin when the length is even.
    /// </summary>
    public static double[] OneSidedFrequencies(int length, double sampleRate) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ValidateSampleRate(sampleRate);
        var result = new double[(length / 2) + 1];
        var binWidth = sampleRate / length;
        for (var index = 0; index < result.Length; index++) {
            result[index] = index * binWidth;
        }

        return result;
    }

    /// <summary>Computes the full complex magnitude spectrum.</summary>
    public static double[] MagnitudeSpectrum(ReadOnlySpan<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        ValidateFinite(values);
        var spectrum = Transform(values);
        var result = new double[spectrum.Length];
        for (var index = 0; index < spectrum.Length; index++) {
            result[index] = spectrum[index].Magnitude;
        }

        return result;
    }

    /// <summary>Computes the full squared-magnitude spectrum.</summary>
    public static double[] PowerSpectrum(ReadOnlySpan<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        ValidateFinite(values);
        var spectrum = Transform(values);
        var result = new double[spectrum.Length];
        for (var index = 0; index < spectrum.Length; index++) {
            result[index] = spectrum[index].MagnitudeSquared;
        }

        return result;
    }

    /// <summary>
    /// Computes the conventional one-sided amplitude spectrum of a real
    /// sequence.
    /// </summary>
    /// <remarks>
    /// The result contains <c>N / 2 + 1</c> bins. Non-DC components are scaled
    /// by <c>2 / N</c>; DC and an even-length Nyquist component are scaled by
    /// <c>1 / N</c>. This is an amplitude convention, not an energy-preserving
    /// normalization.
    /// </remarks>
    public static double[] OneSidedAmplitudeSpectrum(
        ReadOnlySpan<double> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        ValidateFinite(values);
        var complexValues = new Complex[values.Length];
        for (var index = 0; index < values.Length; index++) {
            complexValues[index] = values[index];
        }

        TransformInPlace(complexValues);
        var result = new double[(values.Length / 2) + 1];
        var scale = 1.0 / values.Length;
        result[0] = complexValues[0].Magnitude * scale;
        for (var index = 1; index < result.Length; index++) {
            var isNyquist = values.Length % 2 == 0 &&
                index == result.Length - 1;
            result[index] = complexValues[index].Magnitude *
                scale * (isNyquist ? 1 : 2);
        }

        return result;
    }

    private static void ValidateSampleRate(double sampleRate) {
        if (!double.IsFinite(sampleRate) || sampleRate <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate),
                "The sample rate must be finite and positive.");
        }
    }

    private static void ValidateFinite(ReadOnlySpan<Complex> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!values[index].IsFinite) {
                throw new ArgumentException(
                    "The sequence must contain only finite values.",
                    nameof(values));
            }
        }
    }

    private static void ValidateFinite(ReadOnlySpan<double> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                throw new ArgumentException(
                    "The sequence must contain only finite values.",
                    nameof(values));
            }
        }
    }
}
