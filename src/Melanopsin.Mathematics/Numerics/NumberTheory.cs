// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Integer number-theory algorithms with explicit unsigned magnitude results
/// for operations that can exceed the signed input range.
/// </summary>
public static class NumberTheory {
    private static readonly ulong[] MillerRabinBases = [
        2,
        325,
        9_375,
        28_178,
        450_775,
        9_780_504,
        1_795_265_022,
    ];

    /// <summary>
    /// Computes the greatest common divisor of the magnitudes of two integers.
    /// </summary>
    public static uint Gcd(int left, int right) {
        return (uint)Gcd(
            left < 0 ? unchecked((ulong)-(long)left) : (ulong)left,
            right < 0 ? unchecked((ulong)-(long)right) : (ulong)right);
    }

    /// <summary>
    /// Computes the greatest common divisor of the magnitudes of two integers.
    /// </summary>
    public static ulong Gcd(long left, long right) {
        return Gcd(Magnitude(left), Magnitude(right));
    }

    /// <summary>
    /// Computes the least common multiple of the magnitudes of two integers.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The unsigned result exceeds <see cref="uint.MaxValue"/>.
    /// </exception>
    public static uint Lcm(int left, int right) {
        var result = checked((uint)Lcm(
            left < 0 ? unchecked((ulong)-(long)left) : (ulong)left,
            right < 0 ? unchecked((ulong)-(long)right) : (ulong)right));
        return result;
    }

    /// <summary>
    /// Computes the least common multiple of the magnitudes of two integers.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The unsigned result exceeds <see cref="ulong.MaxValue"/>.
    /// </exception>
    public static ulong Lcm(long left, long right) {
        return Lcm(Magnitude(left), Magnitude(right));
    }

    /// <summary>
    /// Determines whether a non-negative integer is prime.
    /// </summary>
    /// <remarks>
    /// Applies the Miller-Rabin strong probable-prime test described by
    /// <see href="https://doi.org/10.1016/S0022-0000(76)80043-8">Miller (1976)</see>
    /// and <see href="https://doi.org/10.1016/0022-314X(80)90084-0">Rabin (1980)</see>.
    /// The fixed seven-base witness set makes this implementation
    /// deterministic for every <see cref="ulong"/> input.
    /// </remarks>
    public static bool IsPrime(ulong value) {
        if (value < 2) {
            return false;
        }

        if ((value & 1) == 0) {
            return value == 2;
        }

        if (value % 3 == 0) {
            return value == 3;
        }

        var decomposition = value - 1;
        var powerOfTwo = 0;
        while ((decomposition & 1) == 0) {
            decomposition >>= 1;
            powerOfTwo++;
        }

        foreach (var baseValue in MillerRabinBases) {
            if (baseValue % value == 0) {
                continue;
            }

            var witness = ModularPower(baseValue % value, decomposition, value);
            if (witness == 1 || witness == value - 1) {
                continue;
            }

            var composite = true;
            for (var round = 1; round < powerOfTwo; round++) {
                witness = ModularMultiply(witness, witness, value);
                if (witness == value - 1) {
                    composite = false;
                    break;
                }
            }

            if (composite) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the prime factors of a non-negative integer in nondecreasing
    /// order, including multiplicity. Zero and one produce an empty array.
    /// </summary>
    public static ulong[] PrimeFactors(ulong value) {
        if (value < 2) {
            return [];
        }

        var factors = new List<ulong>();
        while ((value & 1) == 0) {
            factors.Add(2);
            value >>= 1;
        }

        while (value % 3 == 0) {
            factors.Add(3);
            value /= 3;
        }

        for (ulong divisor = 5; divisor <= value / divisor; divisor += 6) {
            while (value % divisor == 0) {
                factors.Add(divisor);
                value /= divisor;
            }

            var nextDivisor = divisor + 2;
            while (value % nextDivisor == 0) {
                factors.Add(nextDivisor);
                value /= nextDivisor;
            }
        }

        if (value > 1) {
            factors.Add(value);
        }

        return [.. factors];
    }

    /// <summary>
    /// Computes Euler's totient function for a non-negative integer.
    /// The value for zero is defined as zero.
    /// </summary>
    public static ulong EulerTotient(ulong value) {
        if (value == 0) {
            return 0;
        }

        var result = value;
        var remaining = value;
        if ((remaining & 1) == 0) {
            result -= result / 2;
            while ((remaining & 1) == 0) {
                remaining >>= 1;
            }
        }

        for (ulong divisor = 3; divisor <= remaining / divisor; divisor += 2) {
            if (remaining % divisor != 0) {
                continue;
            }

            result -= result / divisor;
            while (remaining % divisor == 0) {
                remaining /= divisor;
            }
        }

        if (remaining > 1) {
            result -= result / remaining;
        }

        return result;
    }

    /// <summary>
    /// Computes the number of positive divisors of a non-negative integer.
    /// Zero is defined to have zero divisors.
    /// </summary>
    public static ulong DivisorCount(ulong value) {
        if (value == 0) {
            return 0;
        }

        var result = 1UL;
        var remaining = value;
        for (ulong divisor = 2; divisor <= remaining / divisor; divisor++) {
            if (remaining % divisor != 0) {
                continue;
            }

            var exponent = 0UL;
            do {
                remaining /= divisor;
                exponent++;
            } while (remaining % divisor == 0);

            result = checked(result * (exponent + 1));
        }

        if (remaining > 1) {
            result = checked(result * 2);
        }

        return result;
    }

    /// <summary>
    /// Computes the sum of the positive divisors of a non-negative integer.
    /// Zero is defined to have zero divisors.
    /// </summary>
    public static ulong SumOfDivisors(ulong value) {
        if (value == 0) {
            return 0;
        }

        var result = 1UL;
        var remaining = value;
        for (ulong divisor = 2; divisor <= remaining / divisor; divisor++) {
            if (remaining % divisor != 0) {
                continue;
            }

            var divisorPower = divisor;
            var geometricSum = 1UL;
            do {
                remaining /= divisor;
                geometricSum = checked(geometricSum + divisorPower);
                divisorPower = checked(divisorPower * divisor);
            } while (remaining % divisor == 0);

            result = checked(result * geometricSum);
        }

        if (remaining > 1) {
            result = checked(result * (remaining + 1));
        }

        return result;
    }

    private static ulong Gcd(ulong left, ulong right) {
        while (right != 0) {
            (left, right) = (right, left % right);
        }

        return left;
    }

    private static ulong Lcm(ulong left, ulong right) {
        if (left == 0 || right == 0) {
            return 0;
        }

        return checked(left / Gcd(left, right) * right);
    }

    private static ulong Magnitude(long value) {
        return value >= 0
            ? (ulong)value
            : unchecked((ulong)-(value + 1)) + 1;
    }

    private static ulong ModularMultiply(
        ulong left,
        ulong right,
        ulong modulus) {
        return (ulong)(((UInt128)left * right) % modulus);
    }

    private static ulong ModularPower(
        ulong value,
        ulong exponent,
        ulong modulus) {
        var result = 1UL;
        while (exponent != 0) {
            if ((exponent & 1) != 0) {
                result = ModularMultiply(result, value, modulus);
            }

            value = ModularMultiply(value, value, modulus);
            exponent >>= 1;
        }

        return result;
    }
}
