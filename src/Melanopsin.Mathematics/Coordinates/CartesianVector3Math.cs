// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

internal static class CartesianVector3Math {
    public static bool IsFinite(Vector3<double> value) {
        return double.IsFinite(value.X) &&
            double.IsFinite(value.Y) &&
            double.IsFinite(value.Z);
    }

    public static bool TryNormalize(
        Vector3<double> value,
        out Vector3<double> normalized) {
        return TryNormalize(value, out normalized, out _, out _);
    }

    public static bool TryNormalize(
        Vector3<double> value,
        out Vector3<double> normalized,
        out double inverseLength) {
        return TryNormalize(
            value,
            out normalized,
            out inverseLength,
            out _);
    }

    public static bool TryNormalize(
        Vector3<double> value,
        out Vector3<double> normalized,
        out double inverseLength,
        out CoordinateOperationReport report) {
        if (!IsFinite(value)) {
            normalized = default;
            inverseLength = 0;
            report = new CoordinateOperationReport(
                CoordinateOperation.VectorNormalization,
                CoordinateOperationStatus.NonFiniteInput);
            return false;
        }

        var length = ElementaryFunctions.Hypot(value.X, value.Y, value.Z);
        if (!double.IsFinite(length)) {
            normalized = default;
            inverseLength = 0;
            report = new CoordinateOperationReport(
                CoordinateOperation.VectorNormalization,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        if (!(length > 0)) {
            normalized = default;
            inverseLength = 0;
            report = new CoordinateOperationReport(
                CoordinateOperation.VectorNormalization,
                CoordinateOperationStatus.DegenerateGeometry);
            return false;
        }

        inverseLength = 1 / length;
        normalized = new Vector3<double>(
            value.X * inverseLength,
            value.Y * inverseLength,
            value.Z * inverseLength);
        if (!IsFinite(normalized)) {
            normalized = default;
            inverseLength = 0;
            report = new CoordinateOperationReport(
                CoordinateOperation.VectorNormalization,
                CoordinateOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.VectorNormalization,
            CoordinateOperationStatus.Succeeded);
        return true;
    }
}
