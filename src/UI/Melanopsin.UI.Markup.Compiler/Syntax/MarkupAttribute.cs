// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupAttribute(
    int Offset,
    string Name,
    IReadOnlyList<MarkupValuePart> Parts);
