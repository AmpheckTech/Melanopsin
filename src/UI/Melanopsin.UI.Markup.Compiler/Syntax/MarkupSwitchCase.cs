// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupSwitchCase(
    int Offset,
    int PatternOffset,
    string Pattern,
    IReadOnlyList<MarkupNode> Body) : MarkupNode(Offset);
