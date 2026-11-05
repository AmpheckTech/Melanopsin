// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Syntax;

/// <summary>
/// One piece of an attribute value: literal text, an interpolation or a resource reference.
/// <paramref name="Offset"/> is where the piece starts in the markup source.
/// </summary>
internal sealed record MarkupValuePart(string Value, MarkupValuePartKind Kind, int Offset) {
    public bool IsExpression => Kind == MarkupValuePartKind.Expression;

    public bool IsResource => Kind == MarkupValuePartKind.Resource;
}
