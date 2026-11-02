namespace Melanopsin.Graphics.Text;

/// <summary>
/// LB11 through LB19a: word joiners, glue, closing and opening punctuation,
/// and quotation marks.
/// </summary>
internal static partial class LineBreakRules {
    private static bool? PunctuationRule(LineBreakContext context, int index) {
        var before = context.Class(index);
        var after = context.Class(index + 1);
        var afterFlags = context.Flags(index + 1);

        // LB11: word joiners are never separated from their neighbours.
        if (before == LineBreakClass.WJ || after == LineBreakClass.WJ) {
            return false;
        }

        // LB12: never break after a non-breaking space.
        if (before == LineBreakClass.GL) {
            return false;
        }

        // LB12a: never break before a non-breaking space, unless a space
        // or a hyphen-like character precedes it.
        if (after == LineBreakClass.GL &&
            before is not (
                LineBreakClass.SP or
                LineBreakClass.BA or
                LineBreakClass.HY or
                LineBreakClass.HH)) {
            return false;
        }

        // LB13: closing punctuation never starts a line.
        if (after is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.SY) {
            return false;
        }

        var lastNonSpace = context.PreviousNonSpace(index);

        // LB14: opening punctuation never ends a line, even across spaces.
        if (lastNonSpace >= 0 && context.Class(lastNonSpace) == LineBreakClass.OP) {
            return false;
        }

        // LB15a: an initial quotation mark that opens a line stays with
        // what follows it.
        if (lastNonSpace >= 0 &&
            (context.Flags(lastNonSpace) & LineBreakFlags.InitialPunctuation) != 0 &&
            (lastNonSpace == 0 || IsLb15aPredecessor(context.Class(lastNonSpace - 1)))) {
            return false;
        }

        // LB15b: a final quotation mark that closes a line stays with what
        // precedes it.
        if ((afterFlags & LineBreakFlags.FinalPunctuation) != 0 &&
            (index + 2 >= context.Count || IsLb15bSuccessor(context.Class(index + 2)))) {
            return false;
        }

        // LB15c: break before a decimal mark that follows a space.
        if (before == LineBreakClass.SP &&
            after == LineBreakClass.IS &&
            index + 2 < context.Count &&
            context.Class(index + 2) == LineBreakClass.NU) {
            return true;
        }

        // LB15d: punctuation such as a comma never starts a line.
        if (after == LineBreakClass.IS) {
            return false;
        }

        // LB16: closing punctuation stays with a following non-starter.
        if (after == LineBreakClass.NS &&
            lastNonSpace >= 0 &&
            context.Class(lastNonSpace) is LineBreakClass.CL or LineBreakClass.CP) {
            return false;
        }

        // LB17: em dashes stay together, even across spaces.
        if (after == LineBreakClass.B2 &&
            lastNonSpace >= 0 &&
            context.Class(lastNonSpace) == LineBreakClass.B2) {
            return false;
        }

        // LB18: break after spaces.
        if (before == LineBreakClass.SP) {
            return true;
        }

        // LB19: quotation marks stay with the text they quote.
        if (after == LineBreakClass.QU &&
            (afterFlags & LineBreakFlags.InitialPunctuation) == 0) {
            return false;
        }

        if (before == LineBreakClass.QU &&
            (context.Flags(index) & LineBreakFlags.FinalPunctuation) == 0) {
            return false;
        }

        // LB19a: quotation marks in East Asian text may break on either
        // side of the quotation.
        if (after == LineBreakClass.QU &&
            ((context.Flags(index) & LineBreakFlags.EastAsian) == 0 ||
             index + 2 >= context.Count ||
             (context.Flags(index + 2) & LineBreakFlags.EastAsian) == 0)) {
            return false;
        }

        if (before == LineBreakClass.QU &&
            ((afterFlags & LineBreakFlags.EastAsian) == 0 ||
             index == 0 ||
             (context.Flags(index - 1) & LineBreakFlags.EastAsian) == 0)) {
            return false;
        }

        return null;
    }

    private static bool IsLb15aPredecessor(LineBreakClass value) =>
        value is LineBreakClass.BK or
            LineBreakClass.CR or
            LineBreakClass.LF or
            LineBreakClass.NL or
            LineBreakClass.OP or
            LineBreakClass.QU or
            LineBreakClass.GL or
            LineBreakClass.SP or
            LineBreakClass.ZW;

    private static bool IsLb15bSuccessor(LineBreakClass value) =>
        value is LineBreakClass.SP or
            LineBreakClass.GL or
            LineBreakClass.WJ or
            LineBreakClass.CL or
            LineBreakClass.QU or
            LineBreakClass.CP or
            LineBreakClass.EX or
            LineBreakClass.IS or
            LineBreakClass.SY or
            LineBreakClass.BK or
            LineBreakClass.CR or
            LineBreakClass.LF or
            LineBreakClass.NL or
            LineBreakClass.ZW;
}
