namespace Melanopsin.Mathematics;

/// <summary>Diagnostic report for one signal-processing operation.</summary>
public readonly record struct SignalProcessingReport {
    internal SignalProcessingReport(
        SignalOperation operation,
        SignalProcessingMethod method,
        SignalProcessingStatus status,
        int inputLength,
        int outputLength) {
        Operation = operation;
        Method = method;
        Status = status;
        InputLength = inputLength;
        OutputLength = outputLength;
    }

    /// <summary>Gets the executed operation.</summary>
    public SignalOperation Operation { get; }

    /// <summary>Gets the numerical method.</summary>
    public SignalProcessingMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public SignalProcessingStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status is
        SignalProcessingStatus.Succeeded or
        SignalProcessingStatus.NoCompleteFrames;

    /// <summary>Gets the primary input sequence length.</summary>
    public int InputLength { get; }

    /// <summary>Gets the produced output length, which may be zero.</summary>
    public int OutputLength { get; }
}

/// <summary>Diagnostic report for a windowed-sinc FIR design.</summary>
public readonly record struct FirDesignReport {
    internal FirDesignReport(
        SignalOperation operation,
        FilterWindow window,
        SignalProcessingStatus status,
        int tapCount,
        double directCurrentGain) {
        Operation = operation;
        Window = window;
        Status = status;
        TapCount = tapCount;
        DirectCurrentGain = directCurrentGain;
    }

    /// <summary>Gets the requested FIR design kind.</summary>
    public SignalOperation Operation { get; }

    /// <summary>Gets the selected design window.</summary>
    public FilterWindow Window { get; }

    /// <summary>Gets the termination status.</summary>
    public SignalProcessingStatus Status { get; }

    /// <summary>Gets whether a finite tap sequence was produced.</summary>
    public bool Succeeded => Status == SignalProcessingStatus.Succeeded;

    /// <summary>Gets the number of designed taps.</summary>
    public int TapCount { get; }

    /// <summary>Gets the resulting direct-current gain.</summary>
    public double DirectCurrentGain { get; }
}

/// <summary>Diagnostic report for one short-time spectrogram.</summary>
public readonly record struct SpectrogramReport {
    internal SpectrogramReport(
        SignalProcessingReport processing,
        int frameCount,
        int binCount) {
        Processing = processing;
        FrameCount = frameCount;
        BinCount = binCount;
    }

    /// <summary>Gets the common signal-processing report.</summary>
    public SignalProcessingReport Processing { get; }

    /// <summary>Gets the number of complete output frames.</summary>
    public int FrameCount { get; }

    /// <summary>Gets the number of one-sided frequency bins per frame.</summary>
    public int BinCount { get; }

    /// <summary>Gets the operation identifier.</summary>
    public SignalOperation Operation => Processing.Operation;

    /// <summary>Gets the numerical method.</summary>
    public SignalProcessingMethod Method => Processing.Method;

    /// <summary>Gets the termination status.</summary>
    public SignalProcessingStatus Status => Processing.Status;

    /// <summary>
    /// Gets whether the computation completed successfully. Zero complete
    /// frames is a successful, explicitly reported outcome.
    /// </summary>
    public bool Succeeded => Processing.Succeeded;
}
