// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The boundary mode used by discrete convolution.
/// </summary>
public enum ConvolutionMode {
    /// <summary>Returns all <c>signalLength + kernelLength - 1</c> samples.</summary>
    Full,

    /// <summary>
    /// Returns <c>signalLength</c> samples aligned with the input signal.
    /// The kernel is centered using an offset of <c>(kernelLength - 1) / 2</c>.
    /// </summary>
    Same,

    /// <summary>
    /// Returns only positions where the kernel lies completely inside the
    /// signal. An empty result is returned when the kernel is longer.
    /// </summary>
    Valid,
}

public static partial class Signal {
    /// <summary>
    /// Computes discrete convolution of two finite real sequences.
    /// </summary>
    /// <remarks>
    /// The full convolution is
    /// <c>y[n] = sum(k) signal[k] * kernel[n - k]</c>. The same and valid
    /// modes select documented slices of that full result. No boundary
    /// extension is performed.
    /// </remarks>
    public static double[] Convolve(
        ReadOnlySpan<double> signal,
        ReadOnlySpan<double> kernel,
        ConvolutionMode mode = ConvolutionMode.Full) {
        ValidateSignalSequence(signal, nameof(signal));
        ValidateSignalSequence(kernel, nameof(kernel));
        var fullLength = checked(signal.Length + kernel.Length - 1);
        var full = new double[fullLength];
        if (!TryConvolveFull(signal, kernel, full, out var report)) {
            throw new ArithmeticException(
                $"The convolution failed with status {report.Status}.");
        }

        return mode switch {
            ConvolutionMode.Full => full,
            ConvolutionMode.Same => full.AsSpan(
                (kernel.Length - 1) / 2,
                signal.Length).ToArray(),
            ConvolutionMode.Valid => kernel.Length > signal.Length
                ? []
                : full.AsSpan(
                    kernel.Length - 1,
                    signal.Length - kernel.Length + 1).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    /// <summary>
    /// Computes full discrete convolution into caller-owned storage.
    /// </summary>
    /// <param name="signal">A non-empty finite sequence.</param>
    /// <param name="kernel">A non-empty finite sequence.</param>
    /// <param name="destination">
    /// At least <c>signal.Length + kernel.Length - 1</c> values. It must not
    /// overlap either input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the result is finite; otherwise the
    /// destination is cleared and <see langword="false"/> is returned.
    /// </returns>
    public static bool TryConvolveFull(
        ReadOnlySpan<double> signal,
        ReadOnlySpan<double> kernel,
        Span<double> destination) {
        return TryConvolveFull(signal, kernel, destination, out _);
    }

    /// <summary>
    /// Computes full discrete convolution into caller-owned storage and
    /// reports the termination status.
    /// </summary>
    /// <remarks>
    /// A non-finite input is a reported data failure rather than an argument
    /// exception. Buffer-shape and overlap violations remain programming errors
    /// and throw.
    /// </remarks>
    public static bool TryConvolveFull(
        ReadOnlySpan<double> signal,
        ReadOnlySpan<double> kernel,
        Span<double> destination,
        out SignalProcessingReport report) {
        var requiredLength = ValidateConvolutionBuffers(
            signal,
            kernel,
            destination);
        report = new SignalProcessingReport(
            SignalOperation.Convolution,
            SignalProcessingMethod.DirectTimeDomain,
            SignalProcessingStatus.NonFiniteInput,
            signal.Length,
            requiredLength);
        if (!ContainsOnlyFinite(signal) || !ContainsOnlyFinite(kernel)) {
            destination[..requiredLength].Clear();
            return false;
        }

        destination[..requiredLength].Clear();
        for (var signalIndex = 0; signalIndex < signal.Length; signalIndex++) {
            for (var kernelIndex = 0; kernelIndex < kernel.Length; kernelIndex++) {
                destination[signalIndex + kernelIndex] +=
                    signal[signalIndex] * kernel[kernelIndex];
            }
        }

        for (var index = 0; index < requiredLength; index++) {
            if (!double.IsFinite(destination[index])) {
                destination[..requiredLength].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.Convolution,
                    SignalProcessingMethod.DirectTimeDomain,
                    SignalProcessingStatus.NonFiniteOutput,
                    signal.Length,
                    requiredLength);
                return false;
            }
        }

        report = new SignalProcessingReport(
            SignalOperation.Convolution,
            SignalProcessingMethod.DirectTimeDomain,
            SignalProcessingStatus.Succeeded,
            signal.Length,
            requiredLength);
        return true;
    }

    /// <summary>
    /// Computes the full cross-correlation of two real sequences.
    /// </summary>
    /// <remarks>
    /// Result index <c>lag + second.Length - 1</c> contains
    /// <c>sum(first[i + lag] * second[i])</c>, with zero outside the overlapping
    /// range. Positive lags shift the first sequence to the right relative to
    /// the second. The result is not normalized.
    /// </remarks>
    public static double[] CrossCorrelation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        ValidateSignalSequence(first, nameof(first));
        ValidateSignalSequence(second, nameof(second));
        var result = new double[checked(first.Length + second.Length - 1)];
        if (!TryCrossCorrelation(first, second, result, out var report)) {
            throw new ArithmeticException(
                $"The cross-correlation failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Computes full cross-correlation into caller-owned storage.
    /// </summary>
    public static bool TryCrossCorrelation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        Span<double> destination) {
        return TryCrossCorrelation(first, second, destination, out _);
    }

    /// <summary>
    /// Computes full cross-correlation into caller-owned storage and reports
    /// the termination status.
    /// </summary>
    public static bool TryCrossCorrelation(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        Span<double> destination,
        out SignalProcessingReport report) {
        var requiredLength = ValidateConvolutionBuffers(
            first,
            second,
            destination);
        report = new SignalProcessingReport(
            SignalOperation.CrossCorrelation,
            SignalProcessingMethod.DirectTimeDomain,
            SignalProcessingStatus.NonFiniteInput,
            first.Length,
            requiredLength);
        if (!ContainsOnlyFinite(first) || !ContainsOnlyFinite(second)) {
            destination[..requiredLength].Clear();
            return false;
        }

        for (var outputIndex = 0; outputIndex < requiredLength; outputIndex++) {
            var lag = outputIndex - (second.Length - 1);
            var firstStart = Math.Max(0, -lag);
            var secondStart = Math.Max(0, lag);
            var count = Math.Min(
                first.Length - firstStart,
                second.Length - secondStart);
            var sum = 0.0;
            for (var index = 0; index < count; index++) {
                sum += first[firstStart + index] * second[secondStart + index];
            }

            if (!double.IsFinite(sum)) {
                destination[..requiredLength].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.CrossCorrelation,
                    SignalProcessingMethod.DirectTimeDomain,
                    SignalProcessingStatus.NonFiniteOutput,
                    first.Length,
                    requiredLength);
                return false;
            }

            destination[outputIndex] = sum;
        }

        report = new SignalProcessingReport(
            SignalOperation.CrossCorrelation,
            SignalProcessingMethod.DirectTimeDomain,
            SignalProcessingStatus.Succeeded,
            first.Length,
            requiredLength);
        return true;
    }

    /// <summary>Computes the unnormalized autocorrelation of a sequence.</summary>
    public static double[] Autocorrelation(ReadOnlySpan<double> values) {
        return CrossCorrelation(values, values);
    }

    /// <summary>
    /// Applies a causal finite-impulse-response filter.
    /// </summary>
    /// <remarks>
    /// The output is <c>y[n] = sum(k) coefficients[k] * input[n - k]</c>, with
    /// zero-valued input history before the first sample. The output therefore
    /// has the same length as the input and is not centered.
    /// </remarks>
    public static double[] FirFilter(
        ReadOnlySpan<double> input,
        ReadOnlySpan<double> coefficients) {
        ValidateSignalSequence(input, nameof(input));
        ValidateSignalSequence(coefficients, nameof(coefficients));
        var result = new double[input.Length];
        if (!TryFirFilter(input, coefficients, result, out var report)) {
            throw new ArithmeticException(
                $"The FIR filter failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Applies a causal FIR filter into caller-owned storage.
    /// </summary>
    /// <param name="input">A non-empty finite input sequence.</param>
    /// <param name="coefficients">A non-empty finite coefficient sequence.</param>
    /// <param name="destination">
    /// At least <c>input.Length</c> values. It must not overlap either input.
    /// </param>
    public static bool TryFirFilter(
        ReadOnlySpan<double> input,
        ReadOnlySpan<double> coefficients,
        Span<double> destination) {
        return TryFirFilter(input, coefficients, destination, out _);
    }

    /// <summary>
    /// Applies a causal FIR filter into caller-owned storage and reports the
    /// termination status.
    /// </summary>
    public static bool TryFirFilter(
        ReadOnlySpan<double> input,
        ReadOnlySpan<double> coefficients,
        Span<double> destination,
        out SignalProcessingReport report) {
        if (input.IsEmpty) {
            throw new ArgumentException(
                "At least one input value is required.",
                nameof(input));
        }

        if (coefficients.IsEmpty) {
            throw new ArgumentException(
                "At least one coefficient is required.",
                nameof(coefficients));
        }

        if (destination.Length < input.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (input.Overlaps(destination) || coefficients.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        report = new SignalProcessingReport(
            SignalOperation.FirFiltering,
            SignalProcessingMethod.FirRecurrence,
            SignalProcessingStatus.NonFiniteInput,
            input.Length,
            input.Length);
        if (!ContainsOnlyFinite(input) || !ContainsOnlyFinite(coefficients)) {
            destination[..input.Length].Clear();
            return false;
        }

        for (var outputIndex = 0; outputIndex < input.Length; outputIndex++) {
            var sum = 0.0;
            var coefficientCount = Math.Min(
                coefficients.Length,
                outputIndex + 1);
            for (var coefficientIndex = 0;
                coefficientIndex < coefficientCount;
                coefficientIndex++) {
                sum += coefficients[coefficientIndex] *
                    input[outputIndex - coefficientIndex];
            }

            if (!double.IsFinite(sum)) {
                destination[..input.Length].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.FirFiltering,
                    SignalProcessingMethod.FirRecurrence,
                    SignalProcessingStatus.NonFiniteOutput,
                    input.Length,
                    input.Length);
                return false;
            }

            destination[outputIndex] = sum;
        }

        report = new SignalProcessingReport(
            SignalOperation.FirFiltering,
            SignalProcessingMethod.FirRecurrence,
            SignalProcessingStatus.Succeeded,
            input.Length,
            input.Length);
        return true;
    }

    /// <summary>
    /// Computes a causal moving average over complete windows.
    /// </summary>
    /// <remarks>
    /// The result length is <c>input.Length - windowLength + 1</c>, and output
    /// sample <c>i</c> is the mean of input samples
    /// <c>[i, i + windowLength)</c>. No partial boundary windows are emitted.
    /// </remarks>
    public static double[] MovingAverage(
        ReadOnlySpan<double> input,
        int windowLength) {
        ValidateSignalSequence(input, nameof(input));
        ValidateMovingAverageArguments(input.Length, windowLength);
        var result = new double[input.Length - windowLength + 1];
        if (!TryMovingAverage(input, windowLength, result, out var report)) {
            throw new ArithmeticException(
                $"The moving average failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Computes a causal moving average into caller-owned storage and reports
    /// the termination status.
    /// </summary>
    public static bool TryMovingAverage(
        ReadOnlySpan<double> input,
        int windowLength,
        Span<double> destination,
        out SignalProcessingReport report) {
        if (input.IsEmpty) {
            throw new ArgumentException(
                "At least one input value is required.",
                nameof(input));
        }

        ValidateMovingAverageArguments(input.Length, windowLength);
        var resultLength = input.Length - windowLength + 1;
        if (destination.Length < resultLength) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (input.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the input.",
                nameof(destination));
        }

        report = new SignalProcessingReport(
            SignalOperation.MovingAverage,
            SignalProcessingMethod.RunningSum,
            SignalProcessingStatus.NonFiniteInput,
            input.Length,
            resultLength);
        if (!ContainsOnlyFinite(input)) {
            destination[..resultLength].Clear();
            return false;
        }

        var sum = 0.0;
        for (var index = 0; index < windowLength; index++) {
            sum += input[index];
        }

        for (var index = 0; index < resultLength; index++) {
            var value = sum / windowLength;
            if (!double.IsFinite(value)) {
                destination[..resultLength].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.MovingAverage,
                    SignalProcessingMethod.RunningSum,
                    SignalProcessingStatus.NonFiniteOutput,
                    input.Length,
                    resultLength);
                return false;
            }

            destination[index] = value;
            if (index + 1 < resultLength) {
                sum += input[index + windowLength] - input[index];
            }
        }

        report = new SignalProcessingReport(
            SignalOperation.MovingAverage,
            SignalProcessingMethod.RunningSum,
            SignalProcessingStatus.Succeeded,
            input.Length,
            resultLength);
        return true;
    }

    /// <summary>
    /// Resamples a sequence with linear interpolation over the normalized
    /// domain [0, 1].
    /// </summary>
    /// <remarks>
    /// Both input and output retain their first and last samples. Input indices
    /// are mapped as <c>i * (input.Length - 1) / (outputLength - 1)</c>, so
    /// this method does not assume periodic data.
    /// </remarks>
    public static double[] ResampleLinear(
        ReadOnlySpan<double> input,
        int outputLength) {
        ValidateSignalSequence(input, nameof(input));
        if (input.Length < 2) {
            throw new ArgumentException(
                "At least two input values are required.",
                nameof(input));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(outputLength, 2);
        var result = new double[outputLength];
        if (!TryResampleLinear(input, result, out var report)) {
            throw new ArithmeticException(
                $"Linear resampling failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Resamples a sequence with linear interpolation into caller-owned
    /// storage.
    /// </summary>
    public static bool TryResampleLinear(
        ReadOnlySpan<double> input,
        Span<double> destination) {
        return TryResampleLinear(input, destination, out _);
    }

    /// <summary>
    /// Resamples a sequence with linear interpolation into caller-owned
    /// storage and reports the termination status.
    /// </summary>
    public static bool TryResampleLinear(
        ReadOnlySpan<double> input,
        Span<double> destination,
        out SignalProcessingReport report) {
        if (input.Length < 2) {
            throw new ArgumentException(
                "At least two input values are required.",
                nameof(input));
        }

        if (destination.Length < 2) {
            throw new ArgumentException(
                "At least two destination values are required.",
                nameof(destination));
        }

        if (input.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the input.",
                nameof(destination));
        }

        var outputLength = destination.Length;
        report = new SignalProcessingReport(
            SignalOperation.LinearResampling,
            SignalProcessingMethod.LinearInterpolation,
            SignalProcessingStatus.NonFiniteInput,
            input.Length,
            outputLength);
        if (!ContainsOnlyFinite(input)) {
            destination.Clear();
            return false;
        }

        var scale = (double)(input.Length - 1) / (outputLength - 1);
        for (var index = 0; index < outputLength; index++) {
            var position = index * scale;
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = Math.Min(lowerIndex + 1, input.Length - 1);
            var fraction = position - lowerIndex;
            var value = input[lowerIndex] +
                (fraction * (input[upperIndex] - input[lowerIndex]));
            if (!double.IsFinite(value)) {
                destination.Clear();
                report = new SignalProcessingReport(
                    SignalOperation.LinearResampling,
                    SignalProcessingMethod.LinearInterpolation,
                    SignalProcessingStatus.NonFiniteOutput,
                    input.Length,
                    outputLength);
                return false;
            }

            destination[index] = value;
        }

        report = new SignalProcessingReport(
            SignalOperation.LinearResampling,
            SignalProcessingMethod.LinearInterpolation,
            SignalProcessingStatus.Succeeded,
            input.Length,
            outputLength);
        return true;
    }

    /// <summary>
    /// Evaluates one DFT bin with the Goertzel recurrence.
    /// </summary>
    /// <remarks>
    /// The recurrence follows
    /// <see href="https://doi.org/10.2307/2310304">Goertzel (1958)</see>.
    /// </remarks>
    /// <param name="values">A non-empty finite real sequence.</param>
    /// <param name="sampleRate">The sample rate in hertz.</param>
    /// <param name="frequency">The requested frequency in hertz.</param>
    /// <returns>
    /// The unnormalized DFT value using the same sign convention as
    /// <see cref="Fourier"/>.
    /// </returns>
    public static Complex Goertzel(
        ReadOnlySpan<double> values,
        double sampleRate,
        double frequency) {
        ValidateSignalSequence(values, nameof(values));
        ValidateGoertzelArguments(sampleRate, frequency);
        if (!TryGoertzel(
            values,
            sampleRate,
            frequency,
            out var result,
            out var report)) {
            throw new ArithmeticException(
                $"The Goertzel evaluation failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Evaluates one DFT bin with the Goertzel recurrence and reports the
    /// termination status.
    /// </summary>
    public static bool TryGoertzel(
        ReadOnlySpan<double> values,
        double sampleRate,
        double frequency,
        out Complex result,
        out SignalProcessingReport report) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        ValidateGoertzelArguments(sampleRate, frequency);
        result = default;
        report = new SignalProcessingReport(
            SignalOperation.Goertzel,
            SignalProcessingMethod.GoertzelRecurrence,
            SignalProcessingStatus.NonFiniteInput,
            values.Length,
            1);
        if (!ContainsOnlyFinite(values)) {
            return false;
        }

        var angularFrequency = Constants.TwoPi * frequency / sampleRate;
        var coefficient = 2 * Math.Cos(angularFrequency);
        var previous = 0.0;
        var previousPrevious = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var current = values[index] +
                (coefficient * previous) -
                previousPrevious;
            previousPrevious = previous;
            previous = current;
        }

        var cosine = Math.Cos(angularFrequency);
        var sine = Math.Sin(angularFrequency);
        var rawReal = (previous * cosine) - previousPrevious;
        var rawImaginary = previous * sine;
        var phase = angularFrequency * values.Length;
        var phaseCosine = Math.Cos(phase);
        var phaseSine = Math.Sin(phase);
        result = new Complex(
            (rawReal * phaseCosine) + (rawImaginary * phaseSine),
            (rawImaginary * phaseCosine) - (rawReal * phaseSine));
        if (!result.IsFinite) {
            result = default;
            report = new SignalProcessingReport(
                SignalOperation.Goertzel,
                SignalProcessingMethod.GoertzelRecurrence,
                SignalProcessingStatus.NonFiniteOutput,
                values.Length,
                1);
            return false;
        }

        report = new SignalProcessingReport(
            SignalOperation.Goertzel,
            SignalProcessingMethod.GoertzelRecurrence,
            SignalProcessingStatus.Succeeded,
            values.Length,
            1);
        return true;
    }

    private static int ValidateConvolutionBuffers(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        Span<double> destination) {
        if (first.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required in the first sequence.",
                nameof(first));
        }

        if (second.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required in the second sequence.",
                nameof(second));
        }

        var requiredLength = checked(first.Length + second.Length - 1);
        if (destination.Length < requiredLength) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (first.Overlaps(destination) || second.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        return requiredLength;
    }

    private static void ValidateMovingAverageArguments(
        int inputLength,
        int windowLength) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            windowLength,
            inputLength);
    }

    private static void ValidateGoertzelArguments(
        double sampleRate,
        double frequency) {
        if (!double.IsFinite(sampleRate) || sampleRate <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (!double.IsFinite(frequency) ||
            frequency < 0 ||
            frequency > sampleRate * 0.5) {
            throw new ArgumentOutOfRangeException(
                nameof(frequency),
                "The frequency must be finite and between zero and Nyquist.");
        }
    }

    private static bool ContainsOnlyFinite(ReadOnlySpan<double> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static void ValidateSignalSequence(
        ReadOnlySpan<double> values,
        string parameterName) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                parameterName);
        }

        if (!ContainsOnlyFinite(values)) {
            throw new ArgumentException(
                "The sequence must contain only finite values.",
                parameterName);
        }
    }
}
