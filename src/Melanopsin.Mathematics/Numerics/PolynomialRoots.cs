// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Root-finding helpers for polynomial coefficients in ascending degree order.
/// </summary>
public static partial class PolynomialRoots {
    private const int DefaultMaximumIterations = 200;
    private const double DefaultRelativeTolerance = 1e-14;

    /// <summary>
    /// Finds all complex roots of a real polynomial using the simultaneous
    /// Durand-Kerner iteration.
    /// </summary>
    /// <remarks>
    /// The total-step correction is the one analyzed by Kerner:
    /// <see href="https://doi.org/10.1007/BF02162564">Kerner (1966)</see>.
    /// The method is also commonly called the Durand-Kerner method.
    /// </remarks>
    /// <returns>
    /// The number of roots, equal to the polynomial degree. Returns zero for a
    /// constant polynomial, a zero polynomial, or invalid input.
    /// </returns>
    public static int TryFindRoots(
        ReadOnlySpan<double> coefficients,
        Span<Complex> roots,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        return TryFindRoots(
            coefficients,
            roots,
            out _,
            relativeTolerance,
            maximumIterations);
    }

    /// <summary>
    /// Finds all complex roots and returns a detailed termination report.
    /// </summary>
    /// <remarks>
    /// The report distinguishes a zero polynomial, a nonzero constant
    /// polynomial, non-finite coefficients, and iteration failure. No roots are
    /// written unless the status is <see cref="PolynomialRootFindingStatus.Succeeded"/>.
    /// </remarks>
    public static int TryFindRoots(
        ReadOnlySpan<double> coefficients,
        Span<Complex> roots,
        out PolynomialRootFindingReport report,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ValidateTolerance(relativeTolerance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
        if (coefficients.IsEmpty) {
            report = new PolynomialRootFindingReport(
                PolynomialRootFindingMethod.DurandKerner,
                PolynomialRootFindingStatus.InvalidInput,
                -1,
                0,
                0);
            return 0;
        }

        var degree = TrimDegree(coefficients);
        if (degree < 0) {
            report = new PolynomialRootFindingReport(
                PolynomialRootFindingMethod.DurandKerner,
                PolynomialRootFindingStatus.ZeroPolynomial,
                -1,
                0,
                0);
            return 0;
        }

        if (degree == 0) {
            report = new PolynomialRootFindingReport(
                PolynomialRootFindingMethod.DurandKerner,
                PolynomialRootFindingStatus.ConstantPolynomial,
                0,
                0,
                0);
            return 0;
        }

        if (roots.Length < degree) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(roots));
        }

        for (var index = 0; index <= degree; index++) {
            if (!double.IsFinite(coefficients[index])) {
                roots[..degree].Clear();
                report = new PolynomialRootFindingReport(
                    PolynomialRootFindingMethod.DurandKerner,
                    PolynomialRootFindingStatus.InvalidInput,
                    degree,
                    0,
                    0);
                return 0;
            }
        }

        var leading = coefficients[degree];
        var monic = new double[degree + 1];
        for (var index = 0; index <= degree; index++) {
            monic[index] = coefficients[index] / leading;
            if (!double.IsFinite(monic[index])) {
                roots[..degree].Clear();
                report = new PolynomialRootFindingReport(
                    PolynomialRootFindingMethod.DurandKerner,
                    PolynomialRootFindingStatus.InvalidInput,
                    degree,
                    0,
                    0);
                return 0;
            }
        }

        if (degree == 1) {
            roots[0] = new Complex(-monic[0], 0);
            report = new PolynomialRootFindingReport(
                PolynomialRootFindingMethod.DurandKerner,
                PolynomialRootFindingStatus.Succeeded,
                degree,
                1,
                0);
            return 1;
        }

        for (var index = 0; index < degree; index++) {
            roots[index] = Complex.FromPolarCoordinates(
                1,
                Constants.TwoPi * index / degree);
        }

        for (var iteration = 0; iteration < maximumIterations; iteration++) {
            var maximumCorrection = 0.0;
            for (var index = 0; index < degree; index++) {
                var value = EvaluateMonic(monic, roots[index]);
                var product = Complex.One;
                for (var other = 0; other < degree; other++) {
                    if (other != index) {
                        product *= roots[index] - roots[other];
                    }
                }

                if (product == Complex.Zero) {
                    roots[index] += new Complex(
                        relativeTolerance * (index + 1),
                        relativeTolerance * (index + 1));
                    maximumCorrection = 1;
                    continue;
                }

                var correction = value / product;
                roots[index] -= correction;
                maximumCorrection = Math.Max(maximumCorrection, correction.Magnitude);
            }

            var scale = 1.0;
            for (var index = 0; index < degree; index++) {
                scale = Math.Max(scale, roots[index].Magnitude);
            }

            if (maximumCorrection <= relativeTolerance * scale) {
                for (var index = 0; index < degree; index++) {
                    if (!roots[index].IsFinite) {
                        roots[..degree].Clear();
                        report = new PolynomialRootFindingReport(
                            PolynomialRootFindingMethod.DurandKerner,
                            PolynomialRootFindingStatus.NonConvergence,
                            degree,
                            0,
                            iteration + 1);
                        return 0;
                    }
                }

                report = new PolynomialRootFindingReport(
                    PolynomialRootFindingMethod.DurandKerner,
                    PolynomialRootFindingStatus.Succeeded,
                    degree,
                    degree,
                    iteration + 1);
                return degree;
            }
        }

        roots[..degree].Clear();
        report = new PolynomialRootFindingReport(
            PolynomialRootFindingMethod.DurandKerner,
            PolynomialRootFindingStatus.NonConvergence,
            degree,
            0,
            maximumIterations);
        return 0;
    }

    /// <summary>
    /// Computes the two real roots of <c>a x^2 + b x + c</c> when both roots
    /// are real.
    /// </summary>
    /// <returns>The number of real roots; 0 also indicates invalid input.</returns>
    public static int TrySolveQuadraticReal(
        double a,
        double b,
        double c,
        Span<double> roots) {
        return TrySolveQuadraticReal(a, b, c, roots, out _);
    }

    /// <summary>
    /// Computes the real roots of <c>a x^2 + b x + c</c> and reports the
    /// polynomial degree and termination status.
    /// </summary>
    /// <remarks>
    /// Coefficients are trimmed by exact zero identity; no scale-dependent
    /// near-zero test is used. On a non-success status no root is guaranteed.
    /// The stable quadratic formula follows the cancellation-avoiding form in
    /// Press et al., <i>Numerical Recipes</i>, 3rd ed., section 5.6.
    /// </remarks>
    public static int TrySolveQuadraticReal(
        double a,
        double b,
        double c,
        Span<double> roots,
        out PolynomialRootFindingReport report) {
        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c)) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.InvalidInput,
                degree: -1,
                rootCount: 0);
            return 0;
        }

        if (a == 0) {
            if (b == 0) {
                report = QuadraticReport(
                    c == 0
                        ? PolynomialRootFindingStatus.ZeroPolynomial
                        : PolynomialRootFindingStatus.ConstantPolynomial,
                    degree: c == 0 ? -1 : 0,
                    rootCount: 0);
                return 0;
            }

            EnsureRootCapacity(roots);
            var linearRoot = -c / b;
            if (!double.IsFinite(linearRoot)) {
                report = QuadraticReport(
                    PolynomialRootFindingStatus.NumericalFailure,
                    degree: 1,
                    rootCount: 0);
                return 0;
            }

            roots[0] = linearRoot;
            report = QuadraticReport(
                PolynomialRootFindingStatus.Succeeded,
                degree: 1,
                rootCount: 1);
            return 1;
        }

        var discriminant = (b * b) - (4 * a * c);
        if (!double.IsFinite(discriminant)) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.NumericalFailure,
                degree: 2,
                rootCount: 0);
            return 0;
        }

        if (discriminant < 0) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.NoRootsInRequestedDomain,
                degree: 2,
                rootCount: 0);
            return 0;
        }

        EnsureRootCapacity(roots);
        if (discriminant == 0) {
            roots[0] = -b / (2 * a);
            if (!double.IsFinite(roots[0])) {
                report = QuadraticReport(
                    PolynomialRootFindingStatus.NumericalFailure,
                    degree: 2,
                    rootCount: 0);
                return 0;
            }

            report = QuadraticReport(
                PolynomialRootFindingStatus.Succeeded,
                degree: 2,
                rootCount: 1);
            return 1;
        }

        var squareRoot = Math.Sqrt(discriminant);
        var q = -0.5 * (b + Math.CopySign(squareRoot, b));
        roots[0] = q / a;
        roots[1] = c / q;
        if (!double.IsFinite(roots[0]) || !double.IsFinite(roots[1])) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.NumericalFailure,
                degree: 2,
                rootCount: 0);
            return 0;
        }

        report = QuadraticReport(
            PolynomialRootFindingStatus.Succeeded,
            degree: 2,
            rootCount: 2);
        return 2;
    }

    /// <summary>
    /// Computes both complex roots of <c>a x^2 + b x + c</c>.
    /// </summary>
    public static bool TrySolveQuadratic(
        Complex a,
        Complex b,
        Complex c,
        Span<Complex> roots) {
        return TrySolveQuadratic(a, b, c, roots, out _);
    }

    /// <summary>
    /// Computes both complex roots of <c>a z^2 + b z + c</c> and reports the
    /// polynomial degree and termination status.
    /// </summary>
    /// <remarks>
    /// On a non-success status the destination may contain intermediate values;
    /// callers must consult the report before using it. A linear equation still
    /// writes the repeated root twice because the API requests two roots with
    /// multiplicity.
    /// </remarks>
    public static bool TrySolveQuadratic(
        Complex a,
        Complex b,
        Complex c,
        Span<Complex> roots,
        out PolynomialRootFindingReport report) {
        if (!a.IsFinite || !b.IsFinite || !c.IsFinite) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.InvalidInput,
                degree: -1,
                rootCount: 0);
            return false;
        }

        if (roots.Length < 2) {
            throw new ArgumentException(
                "The destination must contain at least two values.",
                nameof(roots));
        }

        if (a == Complex.Zero) {
            if (b == Complex.Zero) {
                report = QuadraticReport(
                    c == Complex.Zero
                        ? PolynomialRootFindingStatus.ZeroPolynomial
                        : PolynomialRootFindingStatus.ConstantPolynomial,
                    degree: c == Complex.Zero ? -1 : 0,
                    rootCount: 0);
                return false;
            }

            var linearRoot = -c / b;
            if (!linearRoot.IsFinite) {
                report = QuadraticReport(
                    PolynomialRootFindingStatus.NumericalFailure,
                    degree: 1,
                    rootCount: 0);
                return false;
            }

            roots[0] = linearRoot;
            roots[1] = linearRoot;
            report = QuadraticReport(
                PolynomialRootFindingStatus.Succeeded,
                degree: 1,
                rootCount: 2);
            return true;
        }

        var discriminant = (b * b) - (4 * a * c);
        var root = ComplexMath.Sqrt(discriminant);
        roots[0] = (-b + root) / (2 * a);
        roots[1] = (-b - root) / (2 * a);
        if (!roots[0].IsFinite || !roots[1].IsFinite) {
            report = QuadraticReport(
                PolynomialRootFindingStatus.NumericalFailure,
                degree: 2,
                rootCount: 0);
            return false;
        }

        report = QuadraticReport(
            PolynomialRootFindingStatus.Succeeded,
            degree: 2,
            rootCount: 2);
        return true;
    }

    private static PolynomialRootFindingReport QuadraticReport(
        PolynomialRootFindingStatus status,
        int degree,
        int rootCount) => new(
            PolynomialRootFindingMethod.Quadratic,
            status,
            degree,
            rootCount,
            iterationCount: 0);

    private static Complex EvaluateMonic(
        ReadOnlySpan<double> coefficients,
        Complex value) {
        var result = Complex.Zero;
        for (var index = coefficients.Length - 1; index >= 0; index--) {
            result = (result * value) + coefficients[index];
        }

        return result;
    }

    private static int TrimDegree(ReadOnlySpan<double> coefficients) {
        var degree = coefficients.Length - 1;
        while (degree >= 0 && coefficients[degree] == 0) {
            degree--;
        }

        return degree;
    }

    private static void EnsureRootCapacity(Span<double> roots) {
        if (roots.Length < 2) {
            throw new ArgumentException(
                "The destination must contain at least two values.",
                nameof(roots));
        }
    }

    private static void ValidateTolerance(double tolerance) {
        if (!double.IsFinite(tolerance) || tolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }
}
