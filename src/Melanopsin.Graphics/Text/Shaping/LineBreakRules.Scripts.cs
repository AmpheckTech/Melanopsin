// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>
/// LB26 through LB30b: Korean syllables, alphabetic scripts and their
/// orthographic syllables, parentheses, and emoji sequences.
/// </summary>
internal static partial class LineBreakRules {
    private const int DottedCircle = 0x25CC;

    private static bool? ScriptRule(LineBreakContext context, int index) {
        var before = context.Class(index);
        var after = context.Class(index + 1);
        var afterFlags = context.Flags(index + 1);

        // LB26: Korean syllables are never broken apart.
        if (before == LineBreakClass.JL &&
            after is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3) {
            return false;
        }

        if (before is LineBreakClass.JV or LineBreakClass.H2 &&
            after is LineBreakClass.JV or LineBreakClass.JT) {
            return false;
        }

        if (before is LineBreakClass.JT or LineBreakClass.H3 && after == LineBreakClass.JT) {
            return false;
        }

        // LB27: Korean syllables behave like ideographs towards numerals.
        if (before is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3 &&
            after == LineBreakClass.PO) {
            return false;
        }

        if (before == LineBreakClass.PR &&
            after is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3) {
            return false;
        }

        // LB28: alphabetics stay together.
        if (before is LineBreakClass.AL or LineBreakClass.HL &&
            after is LineBreakClass.AL or LineBreakClass.HL) {
            return false;
        }

        // LB28a: the orthographic syllables of Brahmic scripts stay whole.
        var beforeSyllable = IsOrthographicSyllable(context, index);
        var afterSyllable = IsOrthographicSyllable(context, index + 1);
        if (before == LineBreakClass.AP && afterSyllable) {
            return false;
        }

        if (beforeSyllable && after is LineBreakClass.VF or LineBreakClass.VI) {
            return false;
        }

        if (beforeSyllable &&
            afterSyllable &&
            index + 2 < context.Count &&
            context.Class(index + 2) == LineBreakClass.VF) {
            return false;
        }

        if (before == LineBreakClass.VI &&
            index > 0 &&
            IsOrthographicSyllable(context, index - 1) &&
            (after == LineBreakClass.AK || context.CodePoint(index + 1) == DottedCircle)) {
            return false;
        }

        // LB29: numeric punctuation stays with a following letter.
        if (before == LineBreakClass.IS && after is LineBreakClass.AL or LineBreakClass.HL) {
            return false;
        }

        // LB30: letters, numbers, and ordinary symbols stay inside
        // parentheses, unless the parenthesis is East Asian.
        if (before is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU &&
            after == LineBreakClass.OP &&
            (afterFlags & LineBreakFlags.EastAsian) == 0) {
            return false;
        }

        if (before == LineBreakClass.CP &&
            (context.Flags(index) & LineBreakFlags.EastAsian) == 0 &&
            after is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU) {
            return false;
        }

        // LB30a: regional indicators pair up.
        if (before == LineBreakClass.RI &&
            after == LineBreakClass.RI &&
            (CountRegionalIndicators(context, index) & 1) != 0) {
            return false;
        }

        // LB30b: emoji modifiers stay with their base.
        if (after == LineBreakClass.EM &&
            (before == LineBreakClass.EB ||
             (context.Flags(index) & LineBreakFlags.PotentialEmoji) != 0)) {
            return false;
        }

        return null;
    }

    private static bool IsOrthographicSyllable(LineBreakContext context, int index) =>
        context.Class(index) is LineBreakClass.AK or LineBreakClass.AS ||
        context.CodePoint(index) == DottedCircle;

    private static int CountRegionalIndicators(LineBreakContext context, int index) {
        var count = 0;
        var candidate = index;
        while (candidate >= 0 && context.Class(candidate) == LineBreakClass.RI) {
            count++;
            candidate--;
        }

        return count;
    }
}
