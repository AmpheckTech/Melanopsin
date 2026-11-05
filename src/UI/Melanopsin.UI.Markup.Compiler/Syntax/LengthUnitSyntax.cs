// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler;

internal static class LengthUnitSyntax {
    // Longer overlapping suffixes must precede their shorter forms.
    public static readonly (string Suffix, string Factory)[] Units = [
        ("rem", "Rem"),
        ("su", "Su"),
        ("ru", "Ru"),
        ("fu", "Fu"),
        ("iu", "Iu"),
        ("hx", "Hx"),
        ("vw", "Vw"),
        ("vh", "Vh"),
        ("fr", "Fr"),
        ("em", "Em"),
        ("dp", "Dp"),
        ("sp", "Sp"),
        ("pt", "Pt"),
        ("px", "Px"),
        ("%", "Percent"),
    ];
}
