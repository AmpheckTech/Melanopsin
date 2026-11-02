namespace Melanopsin.Graphics.Text;

/// <summary>
/// The default line breaking rules of UAX #14, revision 56 (Unicode 17.0),
/// evaluated over the units of a <see cref="LineBreakContext"/>.
/// </summary>
/// <remarks>
/// The first rule that reaches a verdict decides. Each group returns
/// <see langword="null"/> when none of its rules apply, so evaluation falls
/// through to the next group in annex order.
/// </remarks>
internal static partial class LineBreakRules {
    /// <summary>
    /// Returns whether a line may break between the unit at
    /// <paramref name="index"/> and the unit that follows it.
    /// </summary>
    public static bool IsBreakAllowed(LineBreakContext context, int index) {
        // LB4 through LB8a: hard line breaks and zero width characters.
        if (HardBreakRule(context, index) is { } hardBreak) {
            return hardBreak;
        }

        // LB11 through LB19a: glue, punctuation, and quotation.
        if (PunctuationRule(context, index) is { } punctuation) {
            return punctuation;
        }

        // LB20 through LB21b: contingent breaks and hyphens.
        if (HyphenRule(context, index) is { } hyphen) {
            return hyphen;
        }

        // LB22 through LB25: ideographs, letters, and numerals.
        if (NumeralRule(context, index) is { } numeral) {
            return numeral;
        }

        // LB26 through LB30b: Korean syllables, scripts, parentheses, and emoji.
        if (ScriptRule(context, index) is { } script) {
            return script;
        }

        // LB31: break everywhere else.
        return true;
    }
}
