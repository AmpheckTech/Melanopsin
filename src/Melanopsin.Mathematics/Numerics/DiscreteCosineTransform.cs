// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Selects the scaling convention for a discrete cosine transform.
/// </summary>
public enum DctNormalization {
    /// <summary>
    /// Uses the orthonormal DCT-II/III pair, so the transform preserves the
    /// Euclidean norm and its inverse is its transpose.
    /// </summary>
    Orthonormal,

    /// <summary>
    /// Uses <c>X[k] = sum(x[n] cos(pi k (2n + 1) / (2N)))</c> and
    /// <c>x[n] = (X[0] + 2 sum(k=1..N-1, X[k] cos(pi k (2n + 1) / (2N)))) / N</c>.
    /// </summary>
    Unnormalized,
}

/// <summary>
/// Discrete cosine transforms of type II and their inverse type III.
/// </summary>
/// <remarks>
/// Transforms use an even extension and the complex Fourier transform, so their
/// arithmetic cost is <c>O(N log N)</c> for power-of-two lengths. A workspace
/// of exactly <c>2N</c> complex values is accepted by the Try methods. The
/// current sparse-length Fourier fallback allocates internally; callers that
/// require allocation-free execution should use power-of-two lengths. The
/// DCT-II definition follows Ahmed, Natarajan, and Rao (1974),
/// <see href="https://doi.org/10.1109/T-C.1974.223784">doi:10.1109/T-C.1974.223784</see>.
/// </remarks>
public static partial class DiscreteCosineTransform {
    /// <summary>Computes an orthonormal DCT-II.</summary>
    public static double[] Transform(ReadOnlySpan<double> values) {
        return Transform(values, DctNormalization.Orthonormal);
    }

    /// <summary>Computes a DCT-II with an explicit normalization.</summary>
    public static double[] Transform(
        ReadOnlySpan<double> values,
        DctNormalization normalization) {
        ValidateNormalization(normalization);
        var destination = new double[values.Length];
        var workspace = new Complex[checked(2 * values.Length)];
        if (!TryTransform(values, destination, workspace, normalization)) {
            throw new ArithmeticException(
                "The discrete cosine transform produced a non-finite value.");
        }

        return destination;
    }

    /// <summary>Computes a DCT-II into caller-owned buffers.</summary>
    public static bool TryTransform(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        DctNormalization normalization = DctNormalization.Orthonormal) {
        return TryTransform(
            values,
            destination,
            workspace,
            out _,
            normalization);
    }

    private static bool TryTransformCore(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        DctNormalization normalization) {
        var length = values.Length;
        var transformLength = checked(2 * length);
        workspace[..transformLength].Clear();
        for (var index = 0; index < length; index++) {
            var value = values[index];
            if (!double.IsFinite(value)) {
                destination[..length].Clear();
                return false;
            }

            workspace[index] = value;
            workspace[transformLength - 1 - index] = value;
        }

        Fourier.TransformInPlace(workspace[..transformLength]);
        for (var index = 0; index < length; index++) {
            var angle = Constants.Pi * index / transformLength;
            var rotated = workspace[index] *
                new Complex(Math.Cos(angle), -Math.Sin(angle));
            var value = 0.5 * rotated.Real;
            destination[index] = normalization == DctNormalization.Orthonormal
                ? value * OrthonormalScale(index, length)
                : value;
        }

        return ContainsOnlyFinite(destination[..length]);
    }

    /// <summary>Computes an orthonormal inverse DCT (type III).</summary>
    public static double[] InverseTransform(ReadOnlySpan<double> values) {
        return InverseTransform(values, DctNormalization.Orthonormal);
    }

    /// <summary>Computes an inverse DCT with an explicit normalization.</summary>
    public static double[] InverseTransform(
        ReadOnlySpan<double> values,
        DctNormalization normalization) {
        ValidateNormalization(normalization);
        var destination = new double[values.Length];
        var workspace = new Complex[checked(2 * values.Length)];
        if (!TryInverseTransform(
            values,
            destination,
            workspace,
            normalization)) {
            throw new ArithmeticException(
                "The inverse discrete cosine transform produced a non-finite value.");
        }

        return destination;
    }

    /// <summary>Computes an inverse DCT into caller-owned buffers.</summary>
    public static bool TryInverseTransform(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        DctNormalization normalization = DctNormalization.Orthonormal) {
        return TryInverseTransform(
            values,
            destination,
            workspace,
            out _,
            normalization);
    }

    private static bool TryInverseTransformCore(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        DctNormalization normalization) {
        var length = values.Length;
        var transformLength = checked(2 * length);
        workspace[..transformLength].Clear();
        for (var index = 0; index < length; index++) {
            var value = values[index];
            if (!double.IsFinite(value)) {
                destination[..length].Clear();
                return false;
            }

            var coefficient = normalization == DctNormalization.Orthonormal
                ? value / OrthonormalScale(index, length)
                : value;
            var angle = Constants.Pi * index / transformLength;
            workspace[index] = 2 * coefficient *
                new Complex(Math.Cos(angle), Math.Sin(angle));
        }

        for (var index = 1; index < length; index++) {
            workspace[transformLength - index] = workspace[index].Conjugate();
        }

        Fourier.InverseTransformInPlace(workspace[..transformLength]);
        for (var index = 0; index < length; index++) {
            destination[index] = workspace[index].Real;
        }

        return ContainsOnlyFinite(destination[..length]);
    }

    private static double OrthonormalScale(int index, int length) {
        return Math.Sqrt((index == 0 ? 1.0 : 2.0) / length);
    }

    private static void ValidateBuffers(
        ReadOnlySpan<double> values,
        Span<double> destination,
        Span<Complex> workspace,
        DctNormalization normalization) {
        ValidateNormalization(normalization);
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        if (destination.Length < values.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        var transformLength = checked(2 * values.Length);
        if (workspace.Length < transformLength) {
            throw new ArgumentException(
                "The workspace must contain twice as many values as the input.",
                nameof(workspace));
        }

        if (destination.Overlaps(values)) {
            throw new ArgumentException(
                "The input and destination buffers must not overlap.",
                nameof(destination));
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

    private static void ValidateNormalization(DctNormalization normalization) {
        if (normalization is not DctNormalization.Orthonormal and
            not DctNormalization.Unnormalized) {
            throw new ArgumentOutOfRangeException(nameof(normalization));
        }
    }
}
