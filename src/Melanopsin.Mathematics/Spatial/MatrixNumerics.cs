// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

internal static class MatrixNumerics {
    public static double FrobeniusNorm(Matrix2x2 matrix) {
        var sum = Square(matrix.M11) +
            Square(matrix.M12) +
            Square(matrix.M21) +
            Square(matrix.M22);
        return Math.Sqrt(sum);
    }

    public static double FrobeniusNorm(Matrix3x3 matrix) {
        var sum = Square(matrix.M11) +
            Square(matrix.M12) +
            Square(matrix.M13) +
            Square(matrix.M21) +
            Square(matrix.M22) +
            Square(matrix.M23) +
            Square(matrix.M31) +
            Square(matrix.M32) +
            Square(matrix.M33);
        return Math.Sqrt(sum);
    }

    public static double FrobeniusNorm(Matrix4x4 matrix) {
        var sum = Square(matrix.M11) +
            Square(matrix.M12) +
            Square(matrix.M13) +
            Square(matrix.M14) +
            Square(matrix.M21) +
            Square(matrix.M22) +
            Square(matrix.M23) +
            Square(matrix.M24) +
            Square(matrix.M31) +
            Square(matrix.M32) +
            Square(matrix.M33) +
            Square(matrix.M34) +
            Square(matrix.M41) +
            Square(matrix.M42) +
            Square(matrix.M43) +
            Square(matrix.M44);
        return Math.Sqrt(sum);
    }

    public static bool IsRepresentableAsFloat(double value) {
        return double.IsFinite(value) && Math.Abs(value) <= float.MaxValue;
    }

    private static double Square(float value) {
        return (double)value * value;
    }
}
