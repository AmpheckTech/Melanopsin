// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>The termination status of a finite Fourier transform.</summary>
public enum FourierTransformStatus {
    /// <summary>A finite transform was produced.</summary>
    Succeeded,

    /// <summary>The input contained a non-finite real or imaginary component.</summary>
    NonFiniteInput,

    /// <summary>Finite input produced a non-finite transform coefficient.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for one finite Fourier transform.</summary>
public readonly record struct FourierTransformReport {
    internal FourierTransformReport(
        FourierTransformDirection direction,
        FourierTransformAlgorithm algorithm,
        FourierTransformStatus status,
        int length) {
        Direction = direction;
        Algorithm = algorithm;
        Status = status;
        Length = length;
    }

    /// <summary>Gets the transform direction.</summary>
    public FourierTransformDirection Direction { get; }

    /// <summary>Gets the executed algorithm.</summary>
    public FourierTransformAlgorithm Algorithm { get; }

    /// <summary>Gets the termination status.</summary>
    public FourierTransformStatus Status { get; }

    /// <summary>Gets whether the transform succeeded.</summary>
    public bool Succeeded => Status == FourierTransformStatus.Succeeded;

    /// <summary>Gets the transformed sequence length.</summary>
    public int Length { get; }
}

public static partial class Fourier {
    /// <summary>
    /// Computes a forward transform into a destination and reports the exact
    /// algorithm and termination status.
    /// </summary>
    /// <remarks>
    /// Invalid dimensions are programming errors and throw. Non-finite input
    /// and non-finite output are reported without throwing. On failure, the
    /// destination portion corresponding to the transform is cleared.
    /// </remarks>
    public static bool TryTransform(
        ReadOnlySpan<Complex> values,
        Span<Complex> destination,
        out FourierTransformReport report) {
        ValidateTransformBuffers(values, destination);
        var algorithm = FourierCatalog.Recommend(
            values.Length,
            out _);
        report = new FourierTransformReport(
            FourierTransformDirection.Forward,
            algorithm,
            FourierTransformStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            destination[..values.Length].Clear();
            return false;
        }

        values.CopyTo(destination);
        TransformInPlace(destination[..values.Length]);
        if (!ContainsOnlyFinite(destination[..values.Length])) {
            destination[..values.Length].Clear();
            report = new FourierTransformReport(
                FourierTransformDirection.Forward,
                algorithm,
                FourierTransformStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new FourierTransformReport(
            FourierTransformDirection.Forward,
            algorithm,
            FourierTransformStatus.Succeeded,
            values.Length);
        return true;
    }

    /// <summary>
    /// Computes an inverse transform into a destination and reports the exact
    /// algorithm and termination status.
    /// </summary>
    /// <remarks>
    /// Invalid dimensions are programming errors and throw. Non-finite input
    /// and non-finite output are reported without throwing. On failure, the
    /// destination portion corresponding to the transform is cleared.
    /// </remarks>
    public static bool TryInverseTransform(
        ReadOnlySpan<Complex> values,
        Span<Complex> destination,
        out FourierTransformReport report) {
        ValidateTransformBuffers(values, destination);
        var algorithm = FourierCatalog.Recommend(
            values.Length,
            out _);
        report = new FourierTransformReport(
            FourierTransformDirection.Inverse,
            algorithm,
            FourierTransformStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            destination[..values.Length].Clear();
            return false;
        }

        values.CopyTo(destination);
        InverseTransformInPlace(destination[..values.Length]);
        if (!ContainsOnlyFinite(destination[..values.Length])) {
            destination[..values.Length].Clear();
            report = new FourierTransformReport(
                FourierTransformDirection.Inverse,
                algorithm,
                FourierTransformStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new FourierTransformReport(
            FourierTransformDirection.Inverse,
            algorithm,
            FourierTransformStatus.Succeeded,
            values.Length);
        return true;
    }

    /// <summary>
    /// Replaces a sequence with its forward transform and reports the exact
    /// algorithm and termination status.
    /// </summary>
    /// <remarks>
    /// On non-finite output the sequence is cleared. The operation is
    /// in-place and does not allocate for power-of-two lengths.
    /// </remarks>
    public static bool TryTransformInPlace(
        Span<Complex> values,
        out FourierTransformReport report) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var algorithm = FourierCatalog.Recommend(values.Length, out _);
        report = new FourierTransformReport(
            FourierTransformDirection.Forward,
            algorithm,
            FourierTransformStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            values.Clear();
            return false;
        }

        TransformInPlace(values);
        if (!ContainsOnlyFinite(values)) {
            values.Clear();
            report = new FourierTransformReport(
                FourierTransformDirection.Forward,
                algorithm,
                FourierTransformStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new FourierTransformReport(
            FourierTransformDirection.Forward,
            algorithm,
            FourierTransformStatus.Succeeded,
            values.Length);
        return true;
    }

    /// <summary>
    /// Replaces a sequence with its inverse transform and reports the exact
    /// algorithm and termination status.
    /// </summary>
    /// <remarks>
    /// On non-finite output the sequence is cleared. The operation is
    /// in-place and does not allocate for power-of-two lengths.
    /// </remarks>
    public static bool TryInverseTransformInPlace(
        Span<Complex> values,
        out FourierTransformReport report) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var algorithm = FourierCatalog.Recommend(values.Length, out _);
        report = new FourierTransformReport(
            FourierTransformDirection.Inverse,
            algorithm,
            FourierTransformStatus.NonFiniteInput,
            values.Length);
        if (!ContainsOnlyFinite(values)) {
            values.Clear();
            return false;
        }

        InverseTransformInPlace(values);
        if (!ContainsOnlyFinite(values)) {
            values.Clear();
            report = new FourierTransformReport(
                FourierTransformDirection.Inverse,
                algorithm,
                FourierTransformStatus.NonFiniteOutput,
                values.Length);
            return false;
        }

        report = new FourierTransformReport(
            FourierTransformDirection.Inverse,
            algorithm,
            FourierTransformStatus.Succeeded,
            values.Length);
        return true;
    }

    private static void ValidateTransformBuffers(
        ReadOnlySpan<Complex> values,
        Span<Complex> destination) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        if (destination.Length < values.Length) {
            throw new ArgumentException(
                "The destination buffer is too small.",
                nameof(destination));
        }
    }
}
