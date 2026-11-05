namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented cosine-sum window.</summary>
public enum WindowFunctionKind {
    /// <summary>Hann window.</summary>
    Hann,

    /// <summary>Hamming window.</summary>
    Hamming,

    /// <summary>Blackman window.</summary>
    Blackman,

    /// <summary>Four-term Blackman-Harris window.</summary>
    BlackmanHarris,
}

/// <summary>Describes a window-function definition.</summary>
public readonly record struct WindowFunctionDescriptor {
    internal WindowFunctionDescriptor(
        WindowFunctionKind kind,
        string name,
        string cosineSum) {
        Kind = kind;
        Name = name;
        CosineSum = cosineSum;
    }

    /// <summary>Gets the window identifier.</summary>
    public WindowFunctionKind Kind { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the coefficient definition.</summary>
    public string CosineSum { get; }
}

/// <summary>An immutable catalog of implemented window functions.</summary>
public static class WindowFunctionsCatalog {
    private static readonly WindowFunctionDescriptor[] Entries = [
        new(WindowFunctionKind.Hann, "Hann", "0.5 - 0.5 cos(2 pi n / D)"),
        new(WindowFunctionKind.Hamming, "Hamming", "0.54 - 0.46 cos(2 pi n / D)"),
        new(
            WindowFunctionKind.Blackman,
            "Blackman",
            "0.42 - 0.5 cos(2 pi n / D) + 0.08 cos(4 pi n / D)"),
        new(
            WindowFunctionKind.BlackmanHarris,
            "Blackman-Harris",
            "0.35875 - 0.48829 cos(2 pi n / D) + 0.14128 cos(4 pi n / D) - 0.01168 cos(6 pi n / D)"),
    ];

    /// <summary>Gets the built-in window catalog.</summary>
    public static ReadOnlySpan<WindowFunctionDescriptor> Windows => Entries;
}

/// <summary>The termination status of window generation.</summary>
public enum WindowFunctionStatus {
    /// <summary>The requested finite window was produced.</summary>
    Succeeded,
}

/// <summary>Diagnostic report for window generation.</summary>
public readonly record struct WindowFunctionReport {
    internal WindowFunctionReport(
        WindowFunctionKind kind,
        bool periodic,
        WindowFunctionStatus status,
        int length) {
        Kind = kind;
        Periodic = periodic;
        Status = status;
        Length = length;
    }

    /// <summary>Gets the generated window kind.</summary>
    public WindowFunctionKind Kind { get; }

    /// <summary>Gets whether periodic rather than symmetric endpoint scaling was used.</summary>
    public bool Periodic { get; }

    /// <summary>Gets the termination status.</summary>
    public WindowFunctionStatus Status { get; }

    /// <summary>Gets whether generation succeeded.</summary>
    public bool Succeeded => Status == WindowFunctionStatus.Succeeded;

    /// <summary>Gets the generated sample count.</summary>
    public int Length { get; }
}

public static partial class WindowFunctions {
    /// <summary>
    /// Fills a destination with a catalog window and reports its definition.
    /// </summary>
    /// <remarks>
    /// Zero length is valid and produces an empty window. For a periodic
    /// window the cosine denominator is <c>N</c>; for a symmetric window it is
    /// <c>N - 1</c>, except that one- and two-sample degenerate windows are
    /// all ones.
    /// </remarks>
    public static bool TryFill(
        Span<double> destination,
        WindowFunctionKind kind,
        out WindowFunctionReport report,
        bool periodic = true) {
        if (kind is not (WindowFunctionKind.Hann or
            WindowFunctionKind.Hamming or
            WindowFunctionKind.Blackman or
            WindowFunctionKind.BlackmanHarris)) {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var count = periodic ? destination.Length : destination.Length - 1;
        if (destination.IsEmpty || count <= 0) {
            destination.Fill(1);
        } else {
            for (var index = 0; index < destination.Length; index++) {
                destination[index] = Evaluate(kind, index, count);
            }
        }

        report = new WindowFunctionReport(
            kind,
            periodic,
            WindowFunctionStatus.Succeeded,
            destination.Length);
        return true;
    }

    private static double Evaluate(
        WindowFunctionKind kind,
        int index,
        int count) {
        var first = Constants.Tau * index / count;
        return kind switch {
            WindowFunctionKind.Hann =>
                0.5 - (0.5 * Math.Cos(first)),
            WindowFunctionKind.Hamming =>
                0.54 - (0.46 * Math.Cos(first)),
            WindowFunctionKind.Blackman =>
                (0.42 - (0.5 * Math.Cos(first))) +
                    (0.08 * Math.Cos(2 * first)),
            WindowFunctionKind.BlackmanHarris =>
                ((0.35875 - (0.48829 * Math.Cos(first))) +
                    (0.14128 * Math.Cos(2 * first))) -
                    (0.01168 * Math.Cos(3 * first)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
