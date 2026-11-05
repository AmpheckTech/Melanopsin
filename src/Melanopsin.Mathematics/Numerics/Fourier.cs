namespace Melanopsin.Mathematics;

/// <summary>
/// Discrete Fourier transforms and finite convolution.
/// </summary>
/// <remarks>
/// The transform convention is <c>X[k] = sum(x[n] exp(-2 pi i k n / N))</c>.
/// Radix-2 transforms are used for power-of-two lengths. Other lengths use
/// Bluestein's chirp-z reduction to a power-of-two transform, so every finite
/// non-empty sequence is supported without changing convention. The radix-2
/// method follows Cooley and Tukey (1965),
/// <see href="https://doi.org/10.1090/S0025-5718-1965-0178586-1">doi:10.1090/S0025-5718-1965-0178586-1</see>;
/// the arbitrary-length reduction follows Bluestein (1970),
/// <see href="https://doi.org/10.1109/TAU.1970.1162132">doi:10.1109/TAU.1970.1162132</see>.
/// </remarks>
public static partial class Fourier {
    /// <summary>
    /// Computes the forward discrete Fourier transform of a sequence.
    /// </summary>
    public static Complex[] Transform(ReadOnlySpan<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var result = values.ToArray();
        TransformInPlace(result);
        return result;
    }

    /// <summary>
    /// Computes the forward discrete Fourier transform into a destination.
    /// </summary>
    public static bool TryTransform(
        ReadOnlySpan<Complex> values,
        Span<Complex> destination) {
        return TryTransform(values, destination, out _);
    }

    /// <summary>
    /// Computes the inverse discrete Fourier transform of a sequence.
    /// </summary>
    public static Complex[] InverseTransform(ReadOnlySpan<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        var result = new Complex[values.Length];
        for (var index = 0; index < values.Length; index++) {
            result[index] = values[index].Conjugate();
        }

        TransformInPlace(result);
        var scale = 1.0 / result.Length;
        for (var index = 0; index < result.Length; index++) {
            result[index] = new Complex(
                result[index].Real * scale,
                -result[index].Imaginary * scale);
        }

        return result;
    }

    /// <summary>
    /// Replaces a sequence with its forward discrete Fourier transform.
    /// </summary>
    public static void TransformInPlace(Span<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        if (IsPowerOfTwo(values.Length)) {
            TransformRadix2(values);
            return;
        }

        var transformed = TransformBluestein(values);
        transformed.CopyTo(values);
    }

    /// <summary>
    /// Replaces a sequence with its inverse discrete Fourier transform.
    /// </summary>
    public static void InverseTransformInPlace(Span<Complex> values) {
        if (values.IsEmpty) {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }

        for (var index = 0; index < values.Length; index++) {
            values[index] = values[index].Conjugate();
        }

        TransformInPlace(values);
        var scale = 1.0 / values.Length;
        for (var index = 0; index < values.Length; index++) {
            values[index] = new Complex(
                values[index].Real * scale,
                -values[index].Imaginary * scale);
        }
    }

    /// <summary>
    /// Computes the full linear convolution of two complex sequences.
    /// </summary>
    public static Complex[] Convolve(
        ReadOnlySpan<Complex> left,
        ReadOnlySpan<Complex> right) {
        if (left.IsEmpty || right.IsEmpty) {
            throw new ArgumentException(
                "Both input sequences must contain at least one value.",
                left.IsEmpty ? nameof(left) : nameof(right));
        }

        var resultLength = checked(left.Length + right.Length - 1);
        var transformLength = NextPowerOfTwo(resultLength);
        var leftSpectrum = new Complex[transformLength];
        var rightSpectrum = new Complex[transformLength];
        left.CopyTo(leftSpectrum);
        right.CopyTo(rightSpectrum);
        TransformInPlace(leftSpectrum);
        TransformInPlace(rightSpectrum);
        for (var index = 0; index < transformLength; index++) {
            leftSpectrum[index] *= rightSpectrum[index];
        }

        InverseTransformInPlace(leftSpectrum);
        return leftSpectrum[..resultLength];
    }

    /// <summary>
    /// Computes the full linear convolution of two real sequences.
    /// </summary>
    public static double[] Convolve(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right) {
        if (left.IsEmpty || right.IsEmpty) {
            throw new ArgumentException(
                "Both input sequences must contain at least one value.",
                left.IsEmpty ? nameof(left) : nameof(right));
        }

        var complexLeft = new Complex[left.Length];
        var complexRight = new Complex[right.Length];
        for (var index = 0; index < left.Length; index++) {
            complexLeft[index] = left[index];
        }

        for (var index = 0; index < right.Length; index++) {
            complexRight[index] = right[index];
        }

        var convolution = Convolve(complexLeft, complexRight);
        var result = new double[convolution.Length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = convolution[index].Real;
        }

        return result;
    }

    /// <summary>
    /// Computes the valid portion of a real linear convolution. The shorter
    /// input is treated as the kernel; the result has length
    /// <c>max(left.Length, right.Length) - min(left.Length, right.Length) + 1</c>.
    /// </summary>
    public static double[] ConvolveValid(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right) {
        if (left.IsEmpty || right.IsEmpty) {
            throw new ArgumentException(
                "Both input sequences must contain at least one value.",
                left.IsEmpty ? nameof(left) : nameof(right));
        }

        var full = Convolve(left, right);
        var shorter = Math.Min(left.Length, right.Length);
        var resultLength = Math.Max(left.Length, right.Length) - shorter + 1;
        return full.AsSpan(shorter - 1, resultLength).ToArray();
    }

    /// <summary>
    /// Computes the full cross-correlation of two real sequences. The second
    /// sequence is conjugated and reversed before convolution.
    /// </summary>
    public static double[] CrossCorrelate(
        ReadOnlySpan<double> signal,
        ReadOnlySpan<double> kernel) {
        if (signal.IsEmpty || kernel.IsEmpty) {
            throw new ArgumentException(
                "Both input sequences must contain at least one value.",
                signal.IsEmpty ? nameof(signal) : nameof(kernel));
        }

        var reversed = kernel.ToArray();
        Array.Reverse(reversed);
        return Convolve(signal, reversed);
    }

    private static Complex[] TransformBluestein(ReadOnlySpan<Complex> values) {
        var length = values.Length;
        var transformLength = NextPowerOfTwo(checked((2 * length) - 1));
        var chirp = new Complex[length];
        var sequence = new Complex[transformLength];
        var kernel = new Complex[transformLength];
        for (var index = 0; index < length; index++) {
            chirp[index] = Chirp(index, length);
            sequence[index] = values[index] * chirp[index];
            kernel[index] = chirp[index].Conjugate();
            if (index != 0) {
                kernel[transformLength - index] = chirp[index].Conjugate();
            }
        }

        TransformRadix2(sequence);
        TransformRadix2(kernel);
        for (var index = 0; index < transformLength; index++) {
            sequence[index] *= kernel[index];
        }

        InverseTransformRadix2(sequence);
        for (var index = 0; index < length; index++) {
            sequence[index] *= chirp[index];
        }

        return sequence[..length];
    }

    private static Complex Chirp(int index, int length) {
        var period = checked(2L * length);
        var reducedSquare = ((long)index * index) % period;
        var angle = -Constants.Pi * reducedSquare / length;
        return Complex.FromPolarCoordinates(1, angle);
    }

    private static void TransformRadix2(Span<Complex> values) {
        var length = values.Length;
        var last = 0;
        for (var first = 1; first < length; first++) {
            var bit = length >> 1;
            for (; (last & bit) != 0; bit >>= 1) {
                last ^= bit;
            }

            last ^= bit;
            if (first < last) {
                (values[first], values[last]) = (values[last], values[first]);
            }
        }

        for (var blockLength = 2; blockLength <= length; blockLength <<= 1) {
            var angle = -Constants.Tau / blockLength;
            var root = Complex.FromPolarCoordinates(1, angle);
            var halfBlock = blockLength >> 1;
            for (var blockStart = 0; blockStart < length; blockStart += blockLength) {
                var factor = Complex.One;
                for (var offset = 0; offset < halfBlock; offset++) {
                    var evenIndex = blockStart + offset;
                    var oddIndex = evenIndex + halfBlock;
                    var odd = values[oddIndex] * factor;
                    var even = values[evenIndex];
                    values[evenIndex] = even + odd;
                    values[oddIndex] = even - odd;
                    factor *= root;
                }
            }
        }
    }

    private static void InverseTransformRadix2(Span<Complex> values) {
        for (var index = 0; index < values.Length; index++) {
            values[index] = values[index].Conjugate();
        }

        TransformRadix2(values);
        var scale = 1.0 / values.Length;
        for (var index = 0; index < values.Length; index++) {
            values[index] = new Complex(
                values[index].Real * scale,
                -values[index].Imaginary * scale);
        }
    }

    private static bool ContainsOnlyFinite(ReadOnlySpan<Complex> values) {
        for (var index = 0; index < values.Length; index++) {
            if (!values[index].IsFinite) {
                return false;
            }
        }

        return true;
    }

    private static bool IsPowerOfTwo(int value) {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private static int NextPowerOfTwo(int value) {
        if (value <= 1) {
            return 1;
        }

        var result = 1;
        while (result < value) {
            result = checked(result << 1);
        }

        return result;
    }
}
