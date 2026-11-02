namespace Melanopsin.Graphics.Text;

/// <summary>
/// LB22 through LB25: ideographic punctuation, letters and digits, and the
/// numeric sequences built from prefixes, postfixes, and decimal marks.
/// </summary>
internal static partial class LineBreakRules {
    private static bool? NumeralRule(LineBreakContext context, int index) {
        var before = context.Class(index);
        var after = context.Class(index + 1);

        // LB22: ellipses never start a line.
        if (after == LineBreakClass.IN) {
            return false;
        }

        // LB23: letters and digits stay together.
        if (before is LineBreakClass.AL or LineBreakClass.HL && after == LineBreakClass.NU) {
            return false;
        }

        if (before == LineBreakClass.NU && after is LineBreakClass.AL or LineBreakClass.HL) {
            return false;
        }

        // LB23a: numeric prefixes and postfixes stay with ideographs.
        if (before == LineBreakClass.PR &&
            after is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM) {
            return false;
        }

        if (before is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM &&
            after == LineBreakClass.PO) {
            return false;
        }

        // LB24: numeric prefixes and postfixes stay with letters.
        if (before is LineBreakClass.PR or LineBreakClass.PO &&
            after is LineBreakClass.AL or LineBreakClass.HL) {
            return false;
        }

        if (before is LineBreakClass.AL or LineBreakClass.HL &&
            after is LineBreakClass.PR or LineBreakClass.PO) {
            return false;
        }

        // LB25: numbers stay together.
        if (EndsWithNumber(context, index) &&
            after is LineBreakClass.PO or LineBreakClass.PR or LineBreakClass.NU) {
            return false;
        }

        if (before is LineBreakClass.CL or LineBreakClass.CP &&
            after is LineBreakClass.PO or LineBreakClass.PR &&
            IsNumberBeforeClose(context, index)) {
            return false;
        }

        if (before is LineBreakClass.PO or LineBreakClass.PR) {
            if (after == LineBreakClass.NU) {
                return false;
            }

            if (after == LineBreakClass.OP && StartsNumberAfter(context, index + 1)) {
                return false;
            }
        }

        if (before is LineBreakClass.HY or LineBreakClass.IS && after == LineBreakClass.NU) {
            return false;
        }

        return null;
    }

    /// <summary>Matches <c>NU (SY | IS)*</c> ending at the given unit.</summary>
    private static bool EndsWithNumber(LineBreakContext context, int index) {
        var candidate = index;
        while (candidate >= 0 &&
               context.Class(candidate) is LineBreakClass.SY or LineBreakClass.IS) {
            candidate--;
        }

        return candidate >= 0 && context.Class(candidate) == LineBreakClass.NU;
    }

    /// <summary>Matches <c>NU (SY | IS)* (CL | CP)</c> ending at the given unit.</summary>
    private static bool IsNumberBeforeClose(LineBreakContext context, int index) {
        if (context.Class(index) is not (LineBreakClass.CL or LineBreakClass.CP)) {
            return false;
        }

        return EndsWithNumber(context, index - 1);
    }

    /// <summary>Matches <c>(OP | IS)? NU</c> starting at the given unit.</summary>
    private static bool StartsNumberAfter(LineBreakContext context, int index) {
        var candidate = index;
        if (candidate < context.Count && context.Class(candidate) == LineBreakClass.OP) {
            candidate++;
        }

        if (candidate < context.Count && context.Class(candidate) == LineBreakClass.IS) {
            candidate++;
        }

        return candidate < context.Count && context.Class(candidate) == LineBreakClass.NU;
    }
}
