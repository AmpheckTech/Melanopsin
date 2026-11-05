// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class Signal {
    /// <summary>
    /// Computes the root mean square of a finite real sequence.
    /// </summary>
    public static double Rms(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var scale = 0.0;
        var scaledSquareSum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var absolute = Math.Abs(values[index]);
            if (absolute == 0) {
                continue;
            }

            if (scale < absolute) {
                var ratio = scale / absolute;
                scaledSquareSum = 1 + (scaledSquareSum * ratio * ratio);
                scale = absolute;
            } else {
                var ratio = absolute / scale;
                scaledSquareSum += ratio * ratio;
            }
        }

        return scale == 0
            ? 0
            : scale * Math.Sqrt(scaledSquareSum / values.Length);
    }

    /// <summary>
    /// Computes the largest absolute sample value.
    /// </summary>
    public static double PeakAbsolute(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var result = 0.0;
        for (var index = 0; index < values.Length; index++) {
            result = Math.Max(result, Math.Abs(values[index]));
        }

        return result;
    }

    /// <summary>
    /// Computes the crest factor, peak absolute value divided by RMS.
    /// </summary>
    /// <remarks>
    /// An all-zero sequence has no defined crest factor and returns
    /// <see cref="double.NaN"/>. The result can be at most <c>sqrt(n)</c> for
    /// a finite sequence of length <c>n</c>.
    /// </remarks>
    public static double CrestFactor(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var rms = Rms(values);
        return rms == 0 ? double.NaN : PeakAbsolute(values) / rms;
    }

    /// <summary>
    /// Returns the indices immediately following a sign change.
    /// </summary>
    /// <remarks>
    /// Zero samples carry no sign and are skipped. Thus the transition from
    /// <c>+1</c> through zero to <c>-1</c> reports the index of the negative
    /// sample, not the zero sample. A sequence with no completed sign change
    /// returns an empty array.
    /// </remarks>
    public static int[] ZeroCrossingIndices(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var result = new List<int>();
        var hasPreviousSign = false;
        var previousSign = 0;
        for (var index = 0; index < values.Length; index++) {
            var sign = Math.Sign(values[index]);
            if (sign == 0) {
                continue;
            }

            if (hasPreviousSign && sign != previousSign) {
                result.Add(index);
            }

            previousSign = sign;
            hasPreviousSign = true;
        }

        return result.ToArray();
    }

    /// <summary>
    /// Returns the indices of strict interior local maxima.
    /// </summary>
    /// <remarks>
    /// Endpoints are not reported. A plateau with two or more equal maximum
    /// samples is not reported as a local maximum because no single strict
    /// interior peak exists. No smoothing or prominence threshold is applied.
    /// </remarks>
    public static int[] LocalMaximumIndices(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var result = new List<int>();
        for (var index = 1; index < values.Length - 1; index++) {
            if (values[index] > values[index - 1] &&
                values[index] > values[index + 1]) {
                result.Add(index);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Returns the indices of strict interior local minima.
    /// </summary>
    /// <remarks>
    /// Endpoints and flat valleys are not reported. No smoothing or prominence
    /// threshold is applied.
    /// </remarks>
    public static int[] LocalMinimumIndices(ReadOnlySpan<double> values) {
        ValidateSignalSequence(values, nameof(values));
        var result = new List<int>();
        for (var index = 1; index < values.Length - 1; index++) {
            if (values[index] < values[index - 1] &&
                values[index] < values[index + 1]) {
                result.Add(index);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Estimates a one-sided power spectral density with Welch's averaged,
    /// modified-periodogram method.
    /// </summary>
    /// <param name="values">
    /// The finite input sequence. Its length must be at least the window length.
    /// </param>
    /// <param name="window">
    /// A finite non-negative analysis window with positive energy and at least
    /// two samples. The window is applied exactly as supplied; no implicit
    /// window normalization or coherent-gain correction is performed.
    /// </param>
    /// <param name="overlapCount">
    /// The number of overlapping samples between adjacent segments. It must be
    /// non-negative and smaller than the window length. Only complete segments
    /// are included.
    /// </param>
    /// <param name="sampleRate">The sample rate in hertz.</param>
    /// <returns>
    /// A finite one-sided PSD in signal-squared units per hertz with
    /// <c>window.Length / 2 + 1</c> bins. Each segment mean is removed before
    /// windowing. Non-DC bins are doubled, except for the Nyquist bin of an
    /// even-length window, so the integral over the returned bins approximates
    /// signal variance.
    /// </returns>
    public static double[] WelchPowerSpectralDensity(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> window,
        int overlapCount,
        double sampleRate) {
        ValidateSignalSequence(values, nameof(values));
        ValidateWelchArguments(values, window, overlapCount, sampleRate);
        var outputLength = (window.Length / 2) + 1;
        var result = new double[outputLength];
        var workspace = new Complex[window.Length];
        if (!TryWelchPowerSpectralDensity(
            values,
            window,
            overlapCount,
            sampleRate,
            result,
            workspace,
            out var report)) {
            throw new ArithmeticException(
                $"The Welch PSD failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Estimates a one-sided Welch power spectral density into caller-owned
    /// storage and reports the termination status.
    /// </summary>
    /// <param name="values">The finite input sequence.</param>
    /// <param name="window">
    /// The finite, non-negative analysis window with positive energy.
    /// </param>
    /// <param name="overlapCount">The overlap between adjacent segments.</param>
    /// <param name="sampleRate">The positive sample rate in hertz.</param>
    /// <param name="destination">
    /// At least <c>window.Length / 2 + 1</c> values and no overlap with the
    /// input.
    /// </param>
    /// <param name="workspace">
    /// At least <c>window.Length</c> complex values.
    /// </param>
    /// <param name="report">Receives the termination status and dimensions.</param>
    public static bool TryWelchPowerSpectralDensity(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> window,
        int overlapCount,
        double sampleRate,
        Span<double> destination,
        Span<Complex> workspace,
        out SignalProcessingReport report) {
        ValidateWelchArguments(values, window, overlapCount, sampleRate);
        var outputLength = (window.Length / 2) + 1;
        if (destination.Length < outputLength) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (workspace.Length < window.Length) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(workspace));
        }

        if (values.Overlaps(destination) || window.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        report = new SignalProcessingReport(
            SignalOperation.WelchPowerSpectralDensity,
            SignalProcessingMethod.WelchAveraging,
            SignalProcessingStatus.NonFiniteInput,
            values.Length,
            outputLength);
        if (!ContainsOnlyFinite(values) || !ContainsOnlyFinite(window)) {
            destination[..outputLength].Clear();
            return false;
        }

        var windowEnergy = 0.0;
        for (var index = 0; index < window.Length; index++) {
            var value = window[index];
            if (value < 0) {
                destination[..outputLength].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.WelchPowerSpectralDensity,
                    SignalProcessingMethod.WelchAveraging,
                    SignalProcessingStatus.DegenerateInput,
                    values.Length,
                    outputLength);
                return false;
            }

            windowEnergy += value * value;
        }

        if (!(windowEnergy > 0) || !double.IsFinite(windowEnergy)) {
            destination[..outputLength].Clear();
            report = new SignalProcessingReport(
                SignalOperation.WelchPowerSpectralDensity,
                SignalProcessingMethod.WelchAveraging,
                SignalProcessingStatus.DegenerateInput,
                values.Length,
                outputLength);
            return false;
        }

        var segmentLength = window.Length;
        var segmentStep = segmentLength - overlapCount;
        var segmentCount = ((values.Length - segmentLength) / segmentStep) + 1;
        destination[..outputLength].Clear();
        for (var segment = 0; segment < segmentCount; segment++) {
            var offset = segment * segmentStep;
            var mean = 0.0;
            for (var index = 0; index < segmentLength; index++) {
                mean += values[offset + index];
            }

            mean /= segmentLength;
            for (var index = 0; index < segmentLength; index++) {
                workspace[index] = (values[offset + index] - mean) * window[index];
            }

            Fourier.TransformInPlace(workspace[..segmentLength]);
            for (var bin = 0; bin < outputLength; bin++) {
                destination[bin] += workspace[bin].MagnitudeSquared;
            }
        }

        var normalization = 1 /
            (segmentCount * sampleRate * windowEnergy);
        for (var bin = 0; bin < outputLength; bin++) {
            var isUnpaired = bin != 0 &&
                !(segmentLength % 2 == 0 && bin == outputLength - 1);
            destination[bin] *= normalization * (isUnpaired ? 2 : 1);
            if (!double.IsFinite(destination[bin])) {
                destination[..outputLength].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.WelchPowerSpectralDensity,
                    SignalProcessingMethod.WelchAveraging,
                    SignalProcessingStatus.NonFiniteOutput,
                    values.Length,
                    outputLength);
                return false;
            }
        }

        report = new SignalProcessingReport(
            SignalOperation.WelchPowerSpectralDensity,
            SignalProcessingMethod.WelchAveraging,
            SignalProcessingStatus.Succeeded,
            values.Length,
            outputLength);
        return true;
    }

    private static void ValidateWelchArguments(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> window,
        int overlapCount,
        double sampleRate) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one input value is required.",
                nameof(values));
        }

        if (window.Length < 2) {
            throw new ArgumentException(
                "The window must contain at least two values.",
                nameof(window));
        }

        if (window.Length > values.Length) {
            throw new ArgumentException(
                "The window must not be longer than the input sequence.",
                nameof(window));
        }

        if (overlapCount < 0 || overlapCount >= window.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(overlapCount),
                overlapCount,
                "The overlap must be non-negative and smaller than the window.");
        }

        if (!double.IsFinite(sampleRate) || sampleRate <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }
    }
}
