namespace Melanopsin.Mathematics;

/// <summary>
/// Discrete-time signal transforms built on the finite Fourier transform.
/// </summary>
public static partial class Signal {
    /// <summary>
    /// Computes the analytic signal of a real sequence.
    /// </summary>
    /// <remarks>
    /// The returned real part equals the input and the imaginary part is its
    /// Hilbert transform. The transform is evaluated with the DFT convention
    /// documented by <see cref="Fourier"/>.
    /// </remarks>
    public static Complex[] AnalyticSignal(ReadOnlySpan<double> values) {
        var result = new Complex[values.Length];
        var workspace = new Complex[values.Length];
        if (!TryAnalyticSignal(values, result, workspace, out var report)) {
            throw new ArithmeticException(
                $"The analytic signal failed with status {report.Status}.");
        }

        return result;
    }

    /// <summary>
    /// Computes the analytic signal into caller-owned buffers.
    /// </summary>
    /// <param name="values">The finite real sequence.</param>
    /// <param name="destination">At least <c>values.Length</c> values.</param>
    /// <param name="workspace">At least <c>values.Length</c> values.</param>
    public static bool TryAnalyticSignal(
        ReadOnlySpan<double> values,
        Span<Complex> destination,
        Span<Complex> workspace) {
        return TryAnalyticSignal(values, destination, workspace, out _);
    }

    /// <summary>
    /// Computes the analytic signal into caller-owned buffers and reports the
    /// termination status.
    /// </summary>
    /// <remarks>
    /// Buffer-shape and overlap violations are programming errors and throw.
    /// Non-finite values and numerical overflow are reported through
    /// <paramref name="report"/>. A failed operation clears the output region.
    /// </remarks>
    public static bool TryAnalyticSignal(
        ReadOnlySpan<double> values,
        Span<Complex> destination,
        Span<Complex> workspace,
        out SignalProcessingReport report) {
        ValidateAnalyticSignalBuffers(values, destination, workspace);
        var length = values.Length;
        report = new SignalProcessingReport(
            SignalOperation.AnalyticSignal,
            SignalProcessingMethod.FftHilbert,
            SignalProcessingStatus.NonFiniteInput,
            length,
            length);
        for (var index = 0; index < length; index++) {
            if (!double.IsFinite(values[index])) {
                destination[..length].Clear();
                return false;
            }

            workspace[index] = values[index];
        }

        Fourier.TransformInPlace(workspace[..length]);
        var positiveLimit = length / 2;
        for (var index = 1; index <= positiveLimit; index++) {
            var isNyquist = length % 2 == 0 && index == positiveLimit;
            if (!isNyquist) {
                workspace[index] *= 2;
            }
        }

        positiveLimit++;
        for (var index = positiveLimit; index < length; index++) {
            workspace[index] = Complex.Zero;
        }

        Fourier.InverseTransformInPlace(workspace[..length]);
        for (var index = 0; index < length; index++) {
            var value = workspace[index];
            if (!value.IsFinite) {
                destination[..length].Clear();
                report = new SignalProcessingReport(
                    SignalOperation.AnalyticSignal,
                    SignalProcessingMethod.FftHilbert,
                    SignalProcessingStatus.NonFiniteOutput,
                    length,
                    length);
                return false;
            }

            destination[index] = value;
        }

        report = new SignalProcessingReport(
            SignalOperation.AnalyticSignal,
            SignalProcessingMethod.FftHilbert,
            SignalProcessingStatus.Succeeded,
            length,
            length);
        return true;
    }

    /// <summary>Computes the Hilbert transform of a real sequence.</summary>
    public static double[] HilbertTransform(ReadOnlySpan<double> values) {
        var analyticSignal = AnalyticSignal(values);
        var result = new double[analyticSignal.Length];
        for (var index = 0; index < result.Length; index++) {
            result[index] = analyticSignal[index].Imaginary;
        }

        return result;
    }

    private static void ValidateAnalyticSignalBuffers(
        ReadOnlySpan<double> values,
        Span<Complex> destination,
        Span<Complex> workspace) {
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

        if (workspace.Length < values.Length) {
            throw new ArgumentException(
                "The workspace does not contain enough values.",
                nameof(workspace));
        }

        if (destination.Overlaps(workspace)) {
            throw new ArgumentException(
                "The destination and workspace must not overlap.",
                nameof(workspace));
        }
    }
}
