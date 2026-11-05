// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Evaluates the Chebyshev polynomial of the first kind <c>T_n(x)</c>.
    /// </summary>
    /// <remarks>
    /// The three-term recurrence is stable for arguments in [-1, 1] and is
    /// also defined for other finite arguments. Negative degrees are not part
    /// of the API.
    /// </remarks>
    public static double ChebyshevT(int degree, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (degree == 0) {
            return 1;
        }

        var previous = 1.0;
        var current = value;
        for (var index = 2; index <= degree; index++) {
            var next = (2 * value * current) - previous;
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Evaluates the Chebyshev polynomial of the second kind <c>U_n(x)</c>.
    /// </summary>
    public static double ChebyshevU(int degree, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (degree == 0) {
            return 1;
        }

        var previous = 1.0;
        var current = 2 * value;
        for (var index = 2; index <= degree; index++) {
            var next = (2 * value * current) - previous;
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Evaluates the Legendre polynomial <c>P_n(x)</c> using the Bonnet
    /// recurrence.
    /// </summary>
    public static double LegendreP(int degree, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (degree == 0) {
            return 1;
        }

        if (degree == 1) {
            return value;
        }

        var previous = 1.0;
        var current = value;
        for (var index = 2; index <= degree; index++) {
            var next = ((((2 * index) - 1) * value * current) -
                ((index - 1) * previous)) / index;
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Evaluates the associated Legendre function <c>P_n^m(x)</c> on
    /// <c>[-1, 1]</c> with the Condon-Shortley phase.
    /// </summary>
    /// <remarks>
    /// The phase includes the factor <c>(-1)^m</c>. Arguments outside the
    /// closed interval are rejected because the real-valued associated
    /// function is only defined there for all admitted orders.
    /// </remarks>
    public static double AssociatedLegendreP(
        int degree,
        int order,
        double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(order, degree);

        if (double.IsNaN(value) || value < -1 || value > 1) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var root = Math.Sqrt(Math.Max(0, 1 - (value * value)));
        var diagonal = 1.0;
        for (var index = 1; index <= order; index++) {
            diagonal *= -((2 * index) - 1) * root;
        }

        if (degree == order) {
            return diagonal;
        }

        var previous = diagonal;
        var current = value * ((2 * order) + 1) * diagonal;
        if (degree == order + 1) {
            return current;
        }

        for (var index = order + 2; index <= degree; index++) {
            var next =
                ((((2 * index) - 1) * value * current) -
                    ((index + order - 1) * previous)) /
                (index - order);
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Evaluates the physicists' Hermite polynomial <c>H_n(x)</c>.
    /// </summary>
    public static double HermiteH(int degree, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (degree == 0) {
            return 1;
        }

        var previous = 1.0;
        var current = 2 * value;
        for (var index = 2; index <= degree; index++) {
            var next = (2 * value * current) -
                (2 * (index - 1) * previous);
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Evaluates the generalized Laguerre polynomial
    /// <c>L_n^(alpha)(x)</c>.
    /// </summary>
    /// <param name="degree">A non-negative polynomial degree.</param>
    /// <param name="alpha">A finite parameter greater than -1.</param>
    /// <param name="value">The evaluation point.</param>
    public static double LaguerreL(int degree, double alpha, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        if (!(alpha > -1) || !double.IsFinite(alpha)) {
            throw new ArgumentOutOfRangeException(nameof(alpha));
        }

        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (degree == 0) {
            return 1;
        }

        var previous = 1.0;
        var current = 1 + alpha - value;
        for (var index = 2; index <= degree; index++) {
            var next =
                ((((2 * index) - 1 + alpha - value) * current) -
                    ((index - 1 + alpha) * previous)) /
                index;
            previous = current;
            current = next;
        }

        return current;
    }
}
