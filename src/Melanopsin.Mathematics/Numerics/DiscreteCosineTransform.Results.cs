namespace Melanopsin.Mathematics;

/// <summary>Identifies the DCT direction.</summary>
public enum DctDirection {
    /// <summary>DCT-II forward transform.</summary>
    Forward,

    /// <summary>DCT-III inverse transform.</summary>
    Inverse,
}

/// <summary>The termination status of a discrete cosine transform.</summary>
public enum DctStatus {
    /// <summary>A finite transform was produced.</summary>
    Succeeded,

    /// <summary>The input contained a non-finite value.</summary>
    NonFiniteInput,

    /// <summary>Finite input produced a non-finite coefficient.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for one discrete cosine transform.</summary>
public readonly record struct DctReport {
    internal DctReport(
        DctDirection direction,
        DctNormalization normalization,
        DctStatus status,
        int length) {
        Direction = direction;
        Normalization = normalization;
        Status = status;
        Length = length;
    }

    /// <summary>Gets the transform direction.</summary>
    public DctDirection Direction { get; }

    /// <summary>Gets the scaling convention.</summary>
    public DctNormalization Normalization { get; }

    /// <summary>Gets the termination status.</summary>
    public DctStatus Status { get; }

    /// <summary>Gets whether the transform succeeded.</summary>
    public bool Succeeded => Status == DctStatus.Succeeded;

    /// <summary>Gets the transformed sequence length.</summary>
    public int Length { get; }
}

public static partial class DiscreteCosineTransform {
    /// <summary>
    /// Computes an orthonormal DCT-II into caller-owned buffers and reports
    /// the termination status.
    /// </summary>
    public static bool TryTransform(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        out DctReport report,
        DctNormalization normalization = DctNormalization.Orthonormal) {
        ValidateBuffers(values, destination, workspace, normalization);
        report = new DctReport(
            DctDirection.Forward,
            normalization,
            DctStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            destination[..values.Length].Clear();
            return false;
        }

        if (!TryTransformCore(values, destination, workspace, normalization)) {
            report = new DctReport(
                DctDirection.Forward,
                normalization,
                DctStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new DctReport(
            DctDirection.Forward,
            normalization,
            DctStatus.Succeeded,
            values.Length);
        return true;
    }

    /// <summary>
    /// Computes an inverse DCT into caller-owned buffers and reports the
    /// termination status.
    /// </summary>
    public static bool TryInverseTransform(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        out DctReport report,
        DctNormalization normalization = DctNormalization.Orthonormal) {
        ValidateBuffers(values, destination, workspace, normalization);
        report = new DctReport(
            DctDirection.Inverse,
            normalization,
            DctStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            destination[..values.Length].Clear();
            return false;
        }

        if (!TryInverseTransformCore(values, destination, workspace, normalization)) {
            report = new DctReport(
                DctDirection.Inverse,
                normalization,
                DctStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new DctReport(
            DctDirection.Inverse,
            normalization,
            DctStatus.Succeeded,
            values.Length);
        return true;
    }
}
