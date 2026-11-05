// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A real short-time amplitude spectrogram stored in frame-major order.
/// </summary>
public sealed class SpectrogramResult {
    private readonly double[] _frequencies;
    private readonly double[] _magnitudes;

    internal SpectrogramResult(
        double[] frequencies,
        double[] magnitudes,
        SpectrogramReport report) {
        _frequencies = frequencies;
        _magnitudes = magnitudes;
        Report = report;
    }

    /// <summary>Gets the number of complete frames.</summary>
    public int FrameCount => Report.FrameCount;

    /// <summary>Gets the number of one-sided frequency bins per frame.</summary>
    public int BinCount => Report.BinCount;

    /// <summary>Gets the execution report.</summary>
    public SpectrogramReport Report { get; }

    public ReadOnlySpan<double> Frequencies => _frequencies;

    /// <summary>
    /// Gets the frame-major magnitude values in
    /// <c>[frame * binCount + bin]</c> order.
    /// </summary>
    public ReadOnlySpan<double> Magnitudes => _magnitudes;

    public double Magnitude(int frame, int bin) {
        if (frame < 0 || frame >= FrameCount) {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        if (bin < 0 || bin >= BinCount) {
            throw new ArgumentOutOfRangeException(nameof(bin));
        }

        return _magnitudes[(frame * BinCount) + bin];
    }
}

public static partial class Signal {
    /// <summary>
    /// Computes a one-sided short-time amplitude spectrogram over complete
    /// frames.
    /// </summary>
    /// <param name="values">The finite real signal.</param>
    /// <param name="windowLength">The frame length and FFT length.</param>
    /// <param name="hopLength">The positive frame advance.</param>
    /// <param name="sampleRate">The positive sample rate in hertz.</param>
    /// <param name="window">
    /// A finite window with exactly <paramref name="windowLength"/> entries.
    /// It is applied directly; no coherent-gain compensation is performed.
    /// </param>
    /// <param name="removeMean">
    /// When true, each frame mean is subtracted before windowing. The default
    /// is false, so mean removal is never implicit.
    /// </param>
    /// <remarks>
    /// The result has <c>windowLength / 2 + 1</c> bins per frame. Frames that
    /// would extend past the signal are omitted, so an input shorter than the
    /// window produces zero frames. Amplitude scaling follows
    /// <see cref="Fourier.OneSidedAmplitudeSpectrum"/>, but the window gain is
    /// not normalized away.
    /// </remarks>
    public static SpectrogramResult Spectrogram(
        ReadOnlySpan<double> values,
        int windowLength,
        int hopLength,
        double sampleRate,
        ReadOnlySpan<double> window,
        bool removeMean = false) {
        ValidateSignalSequence(values, nameof(values));
        ValidateSpectrogramArguments(
            values,
            windowLength,
            hopLength,
            sampleRate,
            window);
        var frameCount = GetSpectrogramFrameCount(
            values.Length,
            windowLength,
            hopLength);
        var binCount = (windowLength / 2) + 1;
        var magnitudes = new double[checked(frameCount * binCount)];
        var frameWorkspace = new double[windowLength];
        var spectrumWorkspace = new Complex[windowLength];
        if (!TrySpectrogram(
            values,
            windowLength,
            hopLength,
            sampleRate,
            window,
            removeMean,
            magnitudes,
            frameWorkspace,
            spectrumWorkspace,
            out var report)) {
            throw new ArithmeticException(
                $"The spectrogram failed with status {report.Status}.");
        }

        var frequencies = Fourier.OneSidedFrequencies(windowLength, sampleRate);
        return new SpectrogramResult(frequencies, magnitudes, report);
    }

    /// <summary>
    /// Computes a one-sided short-time amplitude spectrogram into caller-owned
    /// storage and reports the termination status.
    /// </summary>
    /// <param name="values">The finite real signal.</param>
    /// <param name="windowLength">The frame length and FFT length.</param>
    /// <param name="hopLength">The positive frame advance.</param>
    /// <param name="sampleRate">The positive sample rate in hertz.</param>
    /// <param name="window">A finite window with exactly the frame length.</param>
    /// <param name="removeMean">Whether to remove each frame mean.</param>
    /// <param name="destination">
    /// At least <c>frameCount * (windowLength / 2 + 1)</c> frame-major values.
    /// A signal shorter than one window produces zero frames and needs no
    /// output storage.
    /// </param>
    /// <param name="frameWorkspace">
    /// At least <c>windowLength</c> real values.
    /// </param>
    /// <param name="spectrumWorkspace">
    /// At least <c>windowLength</c> complex values.
    /// </param>
    /// <param name="report">Receives dimensions and the termination status.</param>
    public static bool TrySpectrogram(
        ReadOnlySpan<double> values,
        int windowLength,
        int hopLength,
        double sampleRate,
        ReadOnlySpan<double> window,
        bool removeMean,
        Span<double> destination,
        Span<double> frameWorkspace,
        Span<Complex> spectrumWorkspace,
        out SpectrogramReport report) {
        ValidateSpectrogramArguments(
            values,
            windowLength,
            hopLength,
            sampleRate,
            window);
        if (frameWorkspace.Length < windowLength) {
            throw new ArgumentException(
                "The frame workspace does not contain enough values.",
                nameof(frameWorkspace));
        }

        if (spectrumWorkspace.Length < windowLength) {
            throw new ArgumentException(
                "The spectrum workspace does not contain enough values.",
                nameof(spectrumWorkspace));
        }

        var frameCount = GetSpectrogramFrameCount(
            values.Length,
            windowLength,
            hopLength);
        var binCount = (windowLength / 2) + 1;
        var outputLength = checked(frameCount * binCount);
        if (destination.Length < outputLength) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        if (values.Overlaps(destination) || window.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap either input.",
                nameof(destination));
        }

        var processing = new SignalProcessingReport(
            SignalOperation.Spectrogram,
            SignalProcessingMethod.ShortTimeFourier,
            SignalProcessingStatus.NonFiniteInput,
            values.Length,
            outputLength);
        report = new SpectrogramReport(processing, frameCount, binCount);
        if (!ContainsOnlyFinite(values) || !ContainsOnlyFinite(window)) {
            destination[..outputLength].Clear();
            return false;
        }

        var frame = frameWorkspace[..windowLength];
        var spectrum = spectrumWorkspace[..windowLength];
        var scale = 1.0 / windowLength;
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++) {
            var start = frameIndex * hopLength;
            values.Slice(start, windowLength).CopyTo(frame);
            if (removeMean) {
                var mean = 0.0;
                for (var index = 0; index < frame.Length; index++) {
                    mean += frame[index];
                }

                mean /= frame.Length;
                for (var index = 0; index < frame.Length; index++) {
                    frame[index] -= mean;
                }
            }

            for (var index = 0; index < frame.Length; index++) {
                spectrum[index] = frame[index] * window[index];
            }

            Fourier.TransformInPlace(spectrum);
            destination[frameIndex * binCount] = spectrum[0].Magnitude * scale;
            for (var bin = 1; bin < binCount; bin++) {
                var isNyquist = windowLength % 2 == 0 &&
                    bin == binCount - 1;
                var magnitude = spectrum[bin].Magnitude *
                    scale * (isNyquist ? 1 : 2);
                if (!double.IsFinite(magnitude)) {
                    destination[..outputLength].Clear();
                    processing = new SignalProcessingReport(
                        SignalOperation.Spectrogram,
                        SignalProcessingMethod.ShortTimeFourier,
                        SignalProcessingStatus.NonFiniteOutput,
                        values.Length,
                        outputLength);
                    report = new SpectrogramReport(processing, frameCount, binCount);
                    return false;
                }

                destination[(frameIndex * binCount) + bin] = magnitude;
            }
        }

        processing = new SignalProcessingReport(
            SignalOperation.Spectrogram,
            SignalProcessingMethod.ShortTimeFourier,
            frameCount == 0
                ? SignalProcessingStatus.NoCompleteFrames
                : SignalProcessingStatus.Succeeded,
            values.Length,
            outputLength);
        report = new SpectrogramReport(processing, frameCount, binCount);
        return true;
    }

    private static int GetSpectrogramFrameCount(
        int inputLength,
        int windowLength,
        int hopLength) {
        return inputLength < windowLength
            ? 0
            : ((inputLength - windowLength) / hopLength) + 1;
    }

    private static void ValidateSpectrogramArguments(
        ReadOnlySpan<double> values,
        int windowLength,
        int hopLength,
        double sampleRate,
        ReadOnlySpan<double> window) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one input value is required.",
                nameof(values));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopLength);
        if (!double.IsFinite(sampleRate) || sampleRate <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (window.Length != windowLength) {
            throw new ArgumentException(
                "The window length must equal the FFT length.",
                nameof(window));
        }
    }
}
