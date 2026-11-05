namespace Melanopsin.Mathematics;

/// <summary>
/// A small, deterministic xoshiro256** pseudo-random generator.
/// </summary>
/// <remarks>
/// The generator stores all state inline and allocates nothing. It is intended
/// for reproducibility and numerical sampling, not cryptographic use. A seed
/// is expanded with SplitMix64; equal seeds produce equal sequences on every
/// supported platform. The state transition is the xoshiro256** algorithm of
/// Blackman and Vigna (2021),
/// <see href="https://doi.org/10.1145/3460772">doi:10.1145/3460772</see>;
/// seed expansion follows Steele, Lea, and Flood (2014),
/// <see href="https://doi.org/10.1145/2660193.2660195">doi:10.1145/2660193.2660195</see>.
/// </remarks>
public record struct Xoshiro256StarStar {
    private ulong _first;
    private ulong _second;
    private ulong _third;
    private ulong _fourth;

    public Xoshiro256StarStar(ulong seed) {
        var splitMixState = seed;
        _first = SplitMix64(ref splitMixState);
        _second = SplitMix64(ref splitMixState);
        _third = SplitMix64(ref splitMixState);
        _fourth = SplitMix64(ref splitMixState);
        if ((_first | _second | _third | _fourth) == 0) {
            _first = 1;
        }
    }

    /// <summary>
    /// Creates a generator from four explicitly supplied state words.
    /// </summary>
    public static Xoshiro256StarStar FromState(
        ulong first,
        ulong second,
        ulong third,
        ulong fourth) {
        if ((first | second | third | fourth) == 0) {
            throw new ArgumentException(
                "The generator state must contain at least one nonzero word.");
        }

        return new Xoshiro256StarStar {
            _first = first,
            _second = second,
            _third = third,
            _fourth = fourth,
        };
    }

    /// <summary>
    /// Exports the four current state words for checkpointing.
    /// </summary>
    public void CopyStateTo(Span<ulong> destination) {
        if (destination.Length < 4) {
            throw new ArgumentException(
                "The destination must contain at least four values.",
                nameof(destination));
        }

        destination[0] = _first;
        destination[1] = _second;
        destination[2] = _third;
        destination[3] = _fourth;
    }

    public ulong NextUInt64() {
        var result = RotateLeft(_second * 5, 7) * 9;
        var temporary = _second << 17;
        _third ^= _first;
        _fourth ^= _second;
        _second ^= _third;
        _first ^= _fourth;
        _third ^= temporary;
        _fourth = RotateLeft(_fourth, 45);
        return result;
    }

    public uint NextUInt32() {
        return (uint)(NextUInt64() >> 32);
    }

    /// <summary>
    /// Produces a double in the half-open interval [0, 1) with 53 random bits.
    /// </summary>
    public double NextDouble() {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Produces a float in the half-open interval [0, 1) with 24 random bits.
    /// </summary>
    public float NextSingle() {
        return (NextUInt32() >> 8) * (1.0f / (1U << 24));
    }

    /// <summary>
    /// Produces an integer in [0, <paramref name="maximumExclusive"/>).
    /// </summary>
    public int NextInt32(int maximumExclusive) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExclusive);
        var range = (uint)maximumExclusive;
        var threshold = unchecked(0U - range) % range;
        uint value;
        do {
            value = NextUInt32();
        } while (value < threshold);

        return (int)(value % range);
    }

    /// <summary>
    /// Produces a non-negative integer below
    /// <paramref name="maximumExclusive"/> without modulo bias.
    /// </summary>
    public long NextInt64(long maximumExclusive) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExclusive);
        var range = (ulong)maximumExclusive;
        var threshold = unchecked(0UL - range) % range;
        ulong value;
        do {
            value = NextUInt64();
        } while (value < threshold);

        return (long)(value % range);
    }

    private static ulong SplitMix64(ref ulong state) {
        state += 0x9E3779B97F4A7C15;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int shift) {
        return (value << shift) | (value >> (64 - shift));
    }
}
