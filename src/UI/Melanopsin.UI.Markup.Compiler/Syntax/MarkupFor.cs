// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupFor(
    int Offset,
    string Variable,
    int IterableOffset,
    string Iterable,
    int TrackOffset,
    string? Track,
    IReadOnlyList<MarkupNode> Body,
    IReadOnlyList<MarkupNode> EmptyBody) : MarkupNode(Offset);
