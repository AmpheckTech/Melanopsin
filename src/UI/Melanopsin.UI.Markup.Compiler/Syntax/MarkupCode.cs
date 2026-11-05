// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Syntax;

/// <summary>C# text captured from an <c>@code</c> block. It is emitted into the
/// component partial class instead of the element tree.</summary>
internal sealed record MarkupCode(int Offset, string Text) : MarkupNode(Offset);
