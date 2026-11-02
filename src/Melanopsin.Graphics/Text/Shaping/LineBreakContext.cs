using System.Buffers;
using System.Text;

namespace Melanopsin.Graphics.Text;

/// <summary>
/// The code points of one paragraph with their line break classes resolved,
/// grouped into the units that the UAX #14 rules of <see cref="LineBreakRules"/>
/// operate on.
/// </summary>
/// <remarks>
/// A unit spans one base code point plus the combining marks and joiners that
/// attach to it, after resolving the ambiguous classes (LB1, LB9, LB10).
/// </remarks>
internal sealed class LineBreakContext {
    private readonly LineBreakClass[] _classes;
    private readonly LineBreakFlags[] _flags;
    private readonly int[] _codePoints;
    private readonly bool[] _endsWithJoiner;
    private readonly int[] _ends;

    public LineBreakContext(ReadOnlySpan<char> span) {
        var offsets = new int[span.Length + 1];
        var classes = new LineBreakClass[span.Length];
        var flags = new LineBreakFlags[span.Length];
        var codePoints = new int[span.Length];
        var count = 0;
        var offset = 0;
        while (offset < span.Length) {
            var status = Rune.DecodeFromUtf16(
                span[offset..],
                out var rune,
                out var consumed);
            if (status != OperationStatus.Done || consumed <= 0) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            offsets[count] = offset;
            codePoints[count] = rune.Value;
            var lineBreakClass = LineBreakData.Resolve(
                rune.Value,
                out var lineBreakFlags);
            classes[count] = ResolveClass(lineBreakClass, lineBreakFlags);
            flags[count] = lineBreakFlags;
            count++;
            offset += consumed;
        }

        offsets[count] = span.Length;

        // LB9/LB10: fold combining marks and zero width joiners into the
        // preceding unit, or treat them as an ordinary letter when they
        // cannot attach to anything.
        _classes = new LineBreakClass[count];
        _flags = new LineBreakFlags[count];
        _codePoints = new int[count];
        _endsWithJoiner = new bool[count];
        _ends = new int[count];
        var unitCount = 0;
        for (var index = 0; index < count; index++) {
            var lineBreakClass = classes[index];
            if (lineBreakClass is LineBreakClass.CM or LineBreakClass.ZWJ &&
                unitCount > 0 &&
                _classes[unitCount - 1] is not (
                    LineBreakClass.BK or
                    LineBreakClass.CR or
                    LineBreakClass.LF or
                    LineBreakClass.NL or
                    LineBreakClass.SP or
                    LineBreakClass.ZW)) {
                _endsWithJoiner[unitCount - 1] =
                    lineBreakClass == LineBreakClass.ZWJ;
                _ends[unitCount - 1] = offsets[index + 1];
                continue;
            }

            // LB10 makes a combining mark that cannot attach to a base an
            // ordinary letter, but LB8a still applies to the joined text,
            // so the unit remembers the joiner it was made of.
            var endsWithJoiner = lineBreakClass == LineBreakClass.ZWJ;
            if (lineBreakClass is LineBreakClass.CM or LineBreakClass.ZWJ) {
                lineBreakClass = LineBreakClass.AL;
                flags[index] = LineBreakFlags.None;
            }

            _classes[unitCount] = lineBreakClass;
            _flags[unitCount] = flags[index];
            _codePoints[unitCount] = codePoints[index];
            _endsWithJoiner[unitCount] = endsWithJoiner;
            _ends[unitCount] = offsets[index + 1];
            unitCount++;
        }

        Count = unitCount;
    }

    /// <summary>The number of units in the paragraph.</summary>
    public int Count { get; }

    /// <summary>The line break class of the unit at <paramref name="index"/>.</summary>
    public LineBreakClass Class(int index) => _classes[index];

    /// <summary>The extra properties of the unit at <paramref name="index"/>.</summary>
    public LineBreakFlags Flags(int index) => _flags[index];

    /// <summary>The base code point of the unit at <paramref name="index"/>.</summary>
    public int CodePoint(int index) => _codePoints[index];

    /// <summary>
    /// Returns whether the unit at <paramref name="index"/> ends with a zero
    /// width joiner that was folded into it.
    /// </summary>
    public bool EndsWithJoiner(int index) => _endsWithJoiner[index];

    /// <summary>The offset, relative to the paragraph, just past a unit.</summary>
    public int EndOf(int index) => _ends[index];

    /// <summary>
    /// Returns the last unit at or before <paramref name="index"/> that is not a
    /// space, or -1 when only spaces precede it.
    /// </summary>
    public int PreviousNonSpace(int index) {
        var candidate = index;
        while (candidate >= 0 && _classes[candidate] == LineBreakClass.SP) {
            candidate--;
        }

        return candidate;
    }

    /// <summary>LB1: resolves the ambiguous classes to their defaults.</summary>
    private static LineBreakClass ResolveClass(
        LineBreakClass lineBreakClass,
        LineBreakFlags flags) => lineBreakClass switch {
            LineBreakClass.AI or LineBreakClass.SG or LineBreakClass.XX => LineBreakClass.AL,
            LineBreakClass.SA => (flags & LineBreakFlags.MarkCategory) != 0
                ? LineBreakClass.CM
                : LineBreakClass.AL,
            LineBreakClass.CJ => LineBreakClass.NS,
            _ => lineBreakClass,
        };
}
