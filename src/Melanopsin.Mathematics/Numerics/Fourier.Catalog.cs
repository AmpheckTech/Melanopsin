namespace Melanopsin.Mathematics;

/// <summary>Identifies the transform direction.</summary>
public enum FourierTransformDirection {
    /// <summary>The forward transform uses the negative exponential sign.</summary>
    Forward,

    /// <summary>The inverse transform uses the positive exponential sign and 1/N scaling.</summary>
    Inverse,
}

/// <summary>Identifies the finite-length Fourier algorithm.</summary>
public enum FourierTransformAlgorithm {
    /// <summary>In-place radix-2 Cooley-Tukey transform.</summary>
    Radix2,

    /// <summary>Bluestein chirp-z reduction for an arbitrary positive length.</summary>
    Bluestein,
}

/// <summary>Explains an automatic Fourier-algorithm selection.</summary>
public enum FourierAlgorithmSelectionReason {
    /// <summary>The length is a power of two.</summary>
    PowerOfTwo,

    /// <summary>The arbitrary length requires Bluestein's reduction.</summary>
    ArbitraryLength,
}

/// <summary>Describes one implemented finite Fourier algorithm.</summary>
public readonly record struct FourierAlgorithmDescriptor {
    internal FourierAlgorithmDescriptor(
        FourierTransformAlgorithm algorithm,
        string name,
        string applicability,
        string workspaceCharacteristic) {
        Algorithm = algorithm;
        Name = name;
        Applicability = applicability;
        WorkspaceCharacteristic = workspaceCharacteristic;
    }

    /// <summary>Gets the algorithm identifier.</summary>
    public FourierTransformAlgorithm Algorithm { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact applicability condition.</summary>
    public string Applicability { get; }

    /// <summary>Gets the execution-memory characteristic.</summary>
    public string WorkspaceCharacteristic { get; }
}

/// <summary>An immutable catalog of finite Fourier algorithms.</summary>
public static class FourierCatalog {
    private static readonly FourierAlgorithmDescriptor[] Entries = [
        new(
            FourierTransformAlgorithm.Radix2,
            "Radix-2 Cooley-Tukey",
            "Positive power-of-two length.",
            "In-place O(1) auxiliary storage and O(N log N) arithmetic."),
        new(
            FourierTransformAlgorithm.Bluestein,
            "Bluestein chirp-z",
            "Any positive length; also valid for powers of two.",
            "Currently allocates O(N) auxiliary complex values and O(N log N) arithmetic."),
    ];

    /// <summary>Gets the built-in algorithm catalog.</summary>
    public static ReadOnlySpan<FourierAlgorithmDescriptor> Algorithms => Entries;

    /// <summary>Selects the algorithm used by the automatic transform path.</summary>
    /// <param name="length">A positive sequence length.</param>
    /// <param name="reason">Receives the selection rationale.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is not positive.
    /// </exception>
    public static FourierTransformAlgorithm Recommend(
        int length,
        out FourierAlgorithmSelectionReason reason) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if ((length & (length - 1)) == 0) {
            reason = FourierAlgorithmSelectionReason.PowerOfTwo;
            return FourierTransformAlgorithm.Radix2;
        }

        reason = FourierAlgorithmSelectionReason.ArbitraryLength;
        return FourierTransformAlgorithm.Bluestein;
    }
}
