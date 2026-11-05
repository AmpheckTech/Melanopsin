namespace Melanopsin.Mathematics;

/// <summary>
/// Internal floating-point primitives shared by numerical kernels.
/// </summary>
internal static class FloatingPointMath {
    /// <summary>
    /// Computes <c>sqrt(x*x + y*y)</c> without avoidable intermediate overflow
    /// or underflow. Non-finite inputs follow IEEE exceptional-value behavior.
    /// </summary>
    public static double Hypot(double x, double y) {
        return double.Hypot(x, y);
    }

    /// <summary>
    /// Computes <c>exp(value) - 1</c> with reduced cancellation near zero.
    /// </summary>
    public static double Expm1(double value) {
        return double.ExpM1(value);
    }

    /// <summary>
    /// Computes <c>log(1 + value)</c> with reduced cancellation near zero.
    /// </summary>
    public static double Log1p(double value) {
        return double.LogP1(value);
    }

    /// <summary>
    /// Adds a finite mathematical amount to a single-precision coordinate and
    /// saturates the representable result at the finite single-precision limits.
    /// </summary>
    public static float SaturatingAdd(float value, double amount) {
        return (float)Math.Clamp(
            (double)value + amount,
            -float.MaxValue,
            float.MaxValue);
    }

    /// <summary>
    /// Converts a finite double result to single precision, saturating values
    /// outside the finite single-precision range instead of producing infinity.
    /// </summary>
    public static float SaturatingConvert(double value) {
        return (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
    }
}
