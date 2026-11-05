// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>LB20 through LB21b: contingent breaks and hyphens.</summary>
internal static partial class LineBreakRules {
    private static bool? HyphenRule(LineBreakContext context, int index) {
        var before = context.Class(index);
        var after = context.Class(index + 1);

        // LB20: unresolved contingent breaks may break on either side.
        if (before == LineBreakClass.CB || after == LineBreakClass.CB) {
            return true;
        }

        // LB20a: a word-initial hyphen stays with the word it introduces.
        if (before is LineBreakClass.HY or LineBreakClass.HH &&
            after is LineBreakClass.AL or LineBreakClass.HL &&
            (index == 0 || IsLb20aPredecessor(context.Class(index - 1)))) {
            return false;
        }

        // LB21: hyphens and non-starters never start a line.
        if (after is LineBreakClass.BA or LineBreakClass.HH or LineBreakClass.HY or LineBreakClass.NS) {
            return false;
        }

        if (before == LineBreakClass.BB) {
            return false;
        }

        // LB21a: a hyphen that follows a Hebrew letter stays with it.
        if (before is LineBreakClass.HY or LineBreakClass.HH &&
            after != LineBreakClass.HL &&
            index > 0 &&
            context.Class(index - 1) == LineBreakClass.HL) {
            return false;
        }

        // LB21b: a solidus stays with a following Hebrew letter.
        if (before == LineBreakClass.SY && after == LineBreakClass.HL) {
            return false;
        }

        return null;
    }

    private static bool IsLb20aPredecessor(LineBreakClass value) =>
        value is LineBreakClass.BK or
            LineBreakClass.CR or
            LineBreakClass.LF or
            LineBreakClass.NL or
            LineBreakClass.SP or
            LineBreakClass.ZW or
            LineBreakClass.CB or
            LineBreakClass.GL;
}
