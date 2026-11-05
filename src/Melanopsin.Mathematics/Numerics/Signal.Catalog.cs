namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented discrete-signal operation.</summary>
public enum SignalOperation {
    /// <summary>Analytic-signal construction through the Hilbert transform.</summary>
    AnalyticSignal,

    /// <summary>Full discrete convolution.</summary>
    Convolution,

    /// <summary>Full cross-correlation.</summary>
    CrossCorrelation,

    /// <summary>Causal finite-impulse-response filtering.</summary>
    FirFiltering,

    /// <summary>Windowed moving average over complete windows.</summary>
    MovingAverage,

    /// <summary>Linear resampling over a normalized domain.</summary>
    LinearResampling,

    /// <summary>Welch averaged power spectral density.</summary>
    WelchPowerSpectralDensity,

    /// <summary>Short-time amplitude spectrogram.</summary>
    Spectrogram,

    /// <summary>Single-bin discrete Fourier transform through Goertzel's recurrence.</summary>
    Goertzel,

    /// <summary>Windowed-sinc low-pass FIR design.</summary>
    LowPassFirDesign,

    /// <summary>Windowed-sinc high-pass FIR design.</summary>
    HighPassFirDesign,

    /// <summary>Windowed-sinc band-pass FIR design.</summary>
    BandPassFirDesign,
}

/// <summary>Identifies the numerical method used by a signal operation.</summary>
public enum SignalProcessingMethod {
    /// <summary>Fast Fourier transform based Hilbert transform.</summary>
    FftHilbert,

    /// <summary>Direct time-domain convolution or correlation.</summary>
    DirectTimeDomain,

    /// <summary>Direct causal FIR recurrence.</summary>
    FirRecurrence,

    /// <summary>Running-sum moving average.</summary>
    RunningSum,

    /// <summary>Linear interpolation between adjacent samples.</summary>
    LinearInterpolation,

    /// <summary>Segmented periodogram averaging.</summary>
    WelchAveraging,

    /// <summary>Framed short-time Fourier analysis.</summary>
    ShortTimeFourier,

    /// <summary>Goertzel's single-bin recurrence.</summary>
    GoertzelRecurrence,

    /// <summary>Windowed ideal-sinc FIR prototype.</summary>
    WindowedSinc,
}

/// <summary>The termination status of a signal-processing operation.</summary>
public enum SignalProcessingStatus {
    /// <summary>The complete result was produced successfully.</summary>
    Succeeded,

    /// <summary>At least one input sample or parameter value was not finite.</summary>
    NonFiniteInput,

    /// <summary>Finite input produced a non-finite result or normalization.</summary>
    NonFiniteOutput,

    /// <summary>The operation is undefined for the supplied degenerate data.</summary>
    DegenerateInput,

    /// <summary>The operation completed but the input contained no complete frame.</summary>
    NoCompleteFrames,

    /// <summary>A numerical accumulation or normalization failed.</summary>
    NumericalFailure,
}

/// <summary>Describes one implemented signal-processing method.</summary>
public readonly record struct SignalOperationDescriptor {
    internal SignalOperationDescriptor(
        SignalOperation operation,
        string name,
        SignalProcessingMethod method,
        string complexity,
        string memoryCharacteristic) {
        Operation = operation;
        Name = name;
        Method = method;
        Complexity = complexity;
        MemoryCharacteristic = memoryCharacteristic;
    }

    /// <summary>Gets the operation identifier.</summary>
    public SignalOperation Operation { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the numerical method.</summary>
    public SignalProcessingMethod Method { get; }

    /// <summary>Gets the documented time-complexity characteristic.</summary>
    public string Complexity { get; }

    /// <summary>Gets the documented allocation and workspace characteristic.</summary>
    public string MemoryCharacteristic { get; }
}

/// <summary>An immutable catalog of implemented signal-processing operations.</summary>
public static class SignalCatalog {
    private static readonly SignalOperationDescriptor[] Entries = [
        new(
            SignalOperation.AnalyticSignal,
            "Analytic signal",
            SignalProcessingMethod.FftHilbert,
            "O(N log N)",
            "Caller-owned complex destination and workspace of length N."),
        new(
            SignalOperation.Convolution,
            "Full convolution",
            SignalProcessingMethod.DirectTimeDomain,
            "O(N M)",
            "Caller-owned output of length N + M - 1; no hidden workspace."),
        new(
            SignalOperation.CrossCorrelation,
            "Full cross-correlation",
            SignalProcessingMethod.DirectTimeDomain,
            "O(N M)",
            "Caller-owned output of length N + M - 1; no hidden workspace."),
        new(
            SignalOperation.FirFiltering,
            "Causal FIR filtering",
            SignalProcessingMethod.FirRecurrence,
            "O(N M)",
            "Caller-owned output of length N; no hidden workspace."),
        new(
            SignalOperation.MovingAverage,
            "Moving average",
            SignalProcessingMethod.RunningSum,
            "O(N)",
            "Caller-owned output; no hidden workspace."),
        new(
            SignalOperation.LinearResampling,
            "Linear resampling",
            SignalProcessingMethod.LinearInterpolation,
            "O(N)",
            "Caller-owned output; no hidden workspace."),
        new(
            SignalOperation.WelchPowerSpectralDensity,
            "Welch PSD",
            SignalProcessingMethod.WelchAveraging,
            "O(K N log N)",
            "Caller-owned complex transform workspace of one segment."),
        new(
            SignalOperation.Spectrogram,
            "Short-time amplitude spectrogram",
            SignalProcessingMethod.ShortTimeFourier,
            "O(F N log N)",
            "Caller-owned frame workspace plus frame-major magnitude output."),
        new(
            SignalOperation.Goertzel,
            "Goertzel DFT bin",
            SignalProcessingMethod.GoertzelRecurrence,
            "O(N)",
            "Constant auxiliary storage."),
        new(
            SignalOperation.LowPassFirDesign,
            "Low-pass FIR design",
            SignalProcessingMethod.WindowedSinc,
            "O(M)",
            "Caller-owned tap destination; no hidden workspace."),
        new(
            SignalOperation.HighPassFirDesign,
            "High-pass FIR design",
            SignalProcessingMethod.WindowedSinc,
            "O(M)",
            "Caller-owned tap destination; no hidden workspace."),
        new(
            SignalOperation.BandPassFirDesign,
            "Band-pass FIR design",
            SignalProcessingMethod.WindowedSinc,
            "O(M)",
            "Caller-owned tap destination; no hidden workspace."),
    ];

    /// <summary>Gets the built-in signal-operation catalog.</summary>
    public static ReadOnlySpan<SignalOperationDescriptor> Operations => Entries;

    /// <summary>Gets the descriptor for an implemented operation.</summary>
    public static SignalOperationDescriptor Get(SignalOperation operation) {
        foreach (var entry in Entries) {
            if (entry.Operation == operation) {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(operation));
    }
}
