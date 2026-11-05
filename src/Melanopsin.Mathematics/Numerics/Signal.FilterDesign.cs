// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A symmetric window used for finite-impulse-response filter design.
/// </summary>
/// <remarks>
/// Windowed-sinc FIR design follows Oppenheim and Schafer, <i>Discrete-Time
/// Signal Processing</i>, 3rd ed. (2010).
/// </remarks>
public enum FilterWindow {
    Rectangular,
    Hann,
    Hamming,
    Blackman,
    BlackmanHarris,
}

public static partial class Signal {
    /// <summary>
    /// Designs a linear-phase low-pass FIR filter using a windowed ideal sinc
    /// response.
    /// </summary>
    /// <param name="tapCount">The positive number of filter taps.</param>
    /// <param name="normalizedCutoff">
    /// The cutoff in cycles per sample, strictly between zero and 0.5.
    /// </param>
    /// <param name="window">The symmetric design window.</param>
    /// <remarks>
    /// The returned taps are causal, in increasing delay order, and normalized
    /// so their sum is one. Pass them to <see cref="FirFilter"/> for direct
    /// convolution. This is a windowed-sinc design and does not claim an
    /// equiripple optimality property.
    /// </remarks>
    public static double[] DesignLowPassFir(
        int tapCount,
        double normalizedCutoff,
        FilterWindow window = FilterWindow.Hamming) {
        var taps = new double[tapCount];
        if (!TryDesignLowPassFir(
            taps,
            tapCount,
            normalizedCutoff,
            window,
            out var report)) {
            throw new ArithmeticException(
                $"The FIR design failed with status {report.Status}.");
        }

        return taps;
    }

    /// <summary>
    /// Designs a low-pass FIR filter into caller-owned storage and reports the
    /// termination status.
    /// </summary>
    public static bool TryDesignLowPassFir(
        Span<double> destination,
        int tapCount,
        double normalizedCutoff,
        FilterWindow window,
        out FirDesignReport report) {
        ValidateFirArguments(tapCount, normalizedCutoff);
        ValidateFilterWindow(window);
        if (destination.Length < tapCount) {
            throw new ArgumentException(
                "The destination does not contain enough taps.",
                nameof(destination));
        }

        var taps = destination[..tapCount];
        FillIdealLowPass(taps, normalizedCutoff);
        ApplyWindow(taps, window);
        var gain = Normalize(taps);
        return CompleteFirDesign(
            SignalOperation.LowPassFirDesign,
            window,
            tapCount,
            gain,
            taps,
            out report);
    }

    /// <summary>
    /// Designs an odd-length linear-phase high-pass FIR filter by spectral
    /// inversion of a low-pass prototype.
    /// </summary>
    /// <remarks>
    /// An odd tap count is required so the spectral inversion has an exact
    /// center sample and the filter can have an exact zero at DC.
    /// </remarks>
    public static double[] DesignHighPassFir(
        int tapCount,
        double normalizedCutoff,
        FilterWindow window = FilterWindow.Hamming) {
        var taps = new double[tapCount];
        if (!TryDesignHighPassFir(
            taps,
            tapCount,
            normalizedCutoff,
            window,
            out var report)) {
            throw new ArithmeticException(
                $"The FIR design failed with status {report.Status}.");
        }

        return taps;
    }

    /// <summary>
    /// Designs a high-pass FIR filter into caller-owned storage and reports
    /// the termination status.
    /// </summary>
    public static bool TryDesignHighPassFir(
        Span<double> destination,
        int tapCount,
        double normalizedCutoff,
        FilterWindow window,
        out FirDesignReport report) {
        ValidateFirArguments(tapCount, normalizedCutoff);
        ValidateFilterWindow(window);
        if ((tapCount & 1) == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(tapCount),
                "A high-pass design requires an odd tap count.");
        }

        if (destination.Length < tapCount) {
            throw new ArgumentException(
                "The destination does not contain enough taps.",
                nameof(destination));
        }

        var taps = destination[..tapCount];
        FillIdealLowPass(taps, normalizedCutoff);
        ApplyWindow(taps, window);
        Normalize(taps);
        for (var index = 0; index < taps.Length; index++) {
            taps[index] = -taps[index];
        }

        taps[tapCount / 2] += 1;
        var gain = Sum(taps);
        return CompleteFirDesign(
            SignalOperation.HighPassFirDesign,
            window,
            tapCount,
            gain,
            taps,
            out report);
    }

    /// <summary>
    /// Designs a linear-phase band-pass FIR filter from the difference of two
    /// windowed ideal low-pass responses.
    /// </summary>
    /// <param name="tapCount">The positive number of filter taps.</param>
    /// <param name="lowerNormalizedCutoff">
    /// The lower cutoff in cycles per sample, strictly between zero and 0.5.
    /// </param>
    /// <param name="upperNormalizedCutoff">
    /// The upper cutoff in cycles per sample, strictly greater than the lower
    /// cutoff and less than 0.5.
    /// </param>
    /// <param name="window">The symmetric design window.</param>
    public static double[] DesignBandPassFir(
        int tapCount,
        double lowerNormalizedCutoff,
        double upperNormalizedCutoff,
        FilterWindow window = FilterWindow.Hamming) {
        var taps = new double[tapCount];
        if (!TryDesignBandPassFir(
            taps,
            tapCount,
            lowerNormalizedCutoff,
            upperNormalizedCutoff,
            window,
            out var report)) {
            throw new ArithmeticException(
                $"The FIR design failed with status {report.Status}.");
        }

        return taps;
    }

    /// <summary>
    /// Designs a band-pass FIR filter into caller-owned storage and reports
    /// the termination status.
    /// </summary>
    public static bool TryDesignBandPassFir(
        Span<double> destination,
        int tapCount,
        double lowerNormalizedCutoff,
        double upperNormalizedCutoff,
        FilterWindow window,
        out FirDesignReport report) {
        ValidateFirArguments(tapCount, lowerNormalizedCutoff);
        ValidateFirArguments(tapCount, upperNormalizedCutoff);
        ValidateFilterWindow(window);
        if (upperNormalizedCutoff <= lowerNormalizedCutoff) {
            throw new ArgumentOutOfRangeException(
                nameof(upperNormalizedCutoff),
                "The upper cutoff must be greater than the lower cutoff.");
        }

        if (destination.Length < tapCount) {
            throw new ArgumentException(
                "The destination does not contain enough taps.",
                nameof(destination));
        }

        var taps = destination[..tapCount];
        var center = (tapCount - 1) * 0.5;
        for (var index = 0; index < tapCount; index++) {
            var offset = index - center;
            taps[index] =
                (2 * upperNormalizedCutoff * Sinc(2 * upperNormalizedCutoff * offset)) -
                (2 * lowerNormalizedCutoff * Sinc(2 * lowerNormalizedCutoff * offset));
        }

        ApplyWindow(taps, window);
        var gain = Sum(taps);
        return CompleteFirDesign(
            SignalOperation.BandPassFirDesign,
            window,
            tapCount,
            gain,
            taps,
            out report);
    }

    private static bool CompleteFirDesign(
        SignalOperation operation,
        FilterWindow window,
        int tapCount,
        double directCurrentGain,
        ReadOnlySpan<double> taps,
        out FirDesignReport report) {
        var status = SignalProcessingStatus.Succeeded;
        if (!double.IsFinite(directCurrentGain) || !ContainsOnlyFinite(taps)) {
            status = SignalProcessingStatus.NonFiniteOutput;
        }

        report = new FirDesignReport(
            operation,
            window,
            status,
            tapCount,
            directCurrentGain);
        return status == SignalProcessingStatus.Succeeded;
    }

    private static void FillIdealLowPass(
        Span<double> taps,
        double normalizedCutoff) {
        var center = (taps.Length - 1) * 0.5;
        for (var index = 0; index < taps.Length; index++) {
            var offset = index - center;
            taps[index] = 2 * normalizedCutoff *
                Sinc(2 * normalizedCutoff * offset);
        }
    }

    private static double Sinc(double value) {
        return value == 0
            ? 1
            : Math.Sin(Constants.Pi * value) / (Constants.Pi * value);
    }

    private static void ApplyWindow(
        Span<double> taps,
        FilterWindow window) {
        switch (window) {
            case FilterWindow.Rectangular:
                return;
            case FilterWindow.Hann:
                WindowFunctions.Hann(taps, periodic: false);
                return;
            case FilterWindow.Hamming:
                WindowFunctions.Hamming(taps, periodic: false);
                return;
            case FilterWindow.Blackman:
                WindowFunctions.Blackman(taps, periodic: false);
                return;
            case FilterWindow.BlackmanHarris:
                WindowFunctions.BlackmanHarris(taps, periodic: false);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(window));
        }
    }

    private static double Normalize(Span<double> taps) {
        var sum = Sum(taps);
        if (!(sum > 0) || !double.IsFinite(sum)) {
            return double.NaN;
        }

        for (var index = 0; index < taps.Length; index++) {
            taps[index] /= sum;
        }

        return 1;
    }

    private static double Sum(ReadOnlySpan<double> values) {
        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            sum += values[index];
        }

        return sum;
    }

    private static void ValidateFirArguments(
        int tapCount,
        double normalizedCutoff) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tapCount);
        if (!double.IsFinite(normalizedCutoff) ||
            normalizedCutoff <= 0 ||
            normalizedCutoff >= 0.5) {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedCutoff),
                "The normalized cutoff must be in (0, 0.5).");
        }
    }

    private static void ValidateFilterWindow(FilterWindow window) {
        if (window is not (FilterWindow.Rectangular or
            FilterWindow.Hann or
            FilterWindow.Hamming or
            FilterWindow.Blackman or
            FilterWindow.BlackmanHarris)) {
            throw new ArgumentOutOfRangeException(nameof(window));
        }
    }
}
