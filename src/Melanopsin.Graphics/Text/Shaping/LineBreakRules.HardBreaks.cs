// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>LB4 through LB8a: hard line breaks and zero width characters.</summary>
internal static partial class LineBreakRules {
    private static bool? HardBreakRule(LineBreakContext context, int index) {
        var before = context.Class(index);
        var after = context.Class(index + 1);

        // LB4/LB5: a hard line break is always followed by a break.
        if (before is LineBreakClass.BK or LineBreakClass.LF or LineBreakClass.NL) {
            return true;
        }

        // LB5: CR LF is one hard line break.
        if (before == LineBreakClass.CR) {
            return after != LineBreakClass.LF;
        }

        // LB6: never break before a hard line break.
        if (after is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL) {
            return false;
        }

        // LB7: never break before a space or a zero width space.
        if (after is LineBreakClass.SP or LineBreakClass.ZW) {
            return false;
        }

        // LB8: break after a zero width space, even across spaces.
        var lastNonSpace = context.PreviousNonSpace(index);
        if (lastNonSpace >= 0 && context.Class(lastNonSpace) == LineBreakClass.ZW) {
            return true;
        }

        // LB8a: never break after a zero width joiner.
        if (context.EndsWithJoiner(index)) {
            return false;
        }

        return null;
    }
}
